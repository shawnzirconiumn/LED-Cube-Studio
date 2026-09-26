using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LedCube.Audio;

internal sealed record PulseCommandResult(bool Succeeded, string StandardOutput, string Error)
{
    public static PulseCommandResult Success(string output = "") => new(true, output, string.Empty);
    public static PulseCommandResult Failure(string error) => new(false, string.Empty, error);
}

internal interface IPulseCommandRunner
{
    Task<PulseCommandResult> RunAsync(CancellationToken cancellationToken, params string[] arguments);
}

internal sealed class PactlCommandRunner : IPulseCommandRunner
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(1);

    public async Task<PulseCommandResult> RunAsync(
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        Process? process = null;
        using var commandCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        commandCancellation.CancelAfter(CommandTimeout);
        try
        {
            var info = new ProcessStartInfo("pactl")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            info.Environment["LC_ALL"] = "C";
            foreach (var argument in arguments) info.ArgumentList.Add(argument);

            process = Process.Start(info);
            if (process is null) return PulseCommandResult.Failure("could not start pactl");

            var outputTask = process.StandardOutput.ReadToEndAsync(commandCancellation.Token);
            var errorTask = process.StandardError.ReadToEndAsync(commandCancellation.Token);
            await process.WaitForExitAsync(commandCancellation.Token).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            var error = (await errorTask.ConfigureAwait(false)).Trim();
            return process.ExitCode == 0
                ? PulseCommandResult.Success(output)
                : PulseCommandResult.Failure(string.IsNullOrWhiteSpace(error)
                    ? $"pactl exited with code {process.ExitCode}"
                    : error);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return PulseCommandResult.Failure($"pactl timed out after {CommandTimeout.TotalSeconds:0.#} seconds");
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch (Exception exception)
        {
            TryKill(process);
            return PulseCommandResult.Failure(exception.Message);
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static void TryKill(Process? process)
    {
        if (process is null) return;
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
            // Best effort only: a failed cleanup must not turn a pactl diagnostic failure into an app failure.
        }
    }
}

internal sealed record LinuxSourceOutputState(
    int Index,
    int ProcessId,
    bool? IsMuted,
    IReadOnlyList<double> ChannelVolumesPercent)
{
    public bool HasUnityGain =>
        ChannelVolumesPercent.Count > 0 &&
        ChannelVolumesPercent.All(volume => Math.Abs(volume - 100) < 0.001);

    public string Describe() =>
        $"source-output #{Index}, parec PID {ProcessId}: " +
        $"mute={IsMuted switch { true => "yes", false => "no", null => "unknown" }}, " +
        $"volume={(ChannelVolumesPercent.Count == 0 ? "unknown" : string.Join("/", ChannelVolumesPercent.Select(
            volume => volume.ToString("0.##'%'", CultureInfo.InvariantCulture))))}";
}

internal static partial class LinuxSourceOutputParser
{
    [GeneratedRegex(@"^Source Output #(\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderPattern();

    [GeneratedRegex(@"(-?\d+(?:\.\d+)?)%", RegexOptions.CultureInvariant)]
    private static partial Regex PercentagePattern();

    public static IReadOnlyList<LinuxSourceOutputState> Parse(string? output)
    {
        var sourceOutputs = new List<LinuxSourceOutputState>();
        SourceOutputBuilder? current = null;
        foreach (var line in (output ?? string.Empty).Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            var trimmed = line.Trim();
            var header = HeaderPattern().Match(trimmed);
            if (header.Success)
            {
                AddCurrent(sourceOutputs, current);
                current = new SourceOutputBuilder(int.Parse(header.Groups[1].Value, CultureInfo.InvariantCulture));
                continue;
            }

            if (current is null) continue;
            if (trimmed.StartsWith("Mute:", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed["Mute:".Length..].Trim();
                current.IsMuted = value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                    ? true
                    : value.Equals("no", StringComparison.OrdinalIgnoreCase) ? false : null;
            }
            else if (trimmed.StartsWith("Volume:", StringComparison.OrdinalIgnoreCase))
            {
                current.ChannelVolumesPercent.Clear();
                foreach (Match match in PercentagePattern().Matches(trimmed["Volume:".Length..]))
                {
                    if (double.TryParse(match.Groups[1].Value, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out var percentage))
                        current.ChannelVolumesPercent.Add(percentage);
                }
            }
            else if (trimmed.StartsWith("application.process.id", StringComparison.Ordinal))
            {
                var separator = trimmed.IndexOf('=');
                var value = separator >= 0 ? trimmed[(separator + 1)..].Trim().Trim('"') : string.Empty;
                if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var processId))
                    current.ProcessId = processId;
            }
        }

        AddCurrent(sourceOutputs, current);
        return sourceOutputs;
    }

    private static void AddCurrent(ICollection<LinuxSourceOutputState> sourceOutputs, SourceOutputBuilder? current)
    {
        if (current?.ProcessId is not { } processId) return;
        sourceOutputs.Add(new LinuxSourceOutputState(
            current.Index,
            processId,
            current.IsMuted,
            current.ChannelVolumesPercent.ToArray()));
    }

    private sealed class SourceOutputBuilder(int index)
    {
        public int Index { get; } = index;
        public int? ProcessId { get; set; }
        public bool? IsMuted { get; set; }
        public List<double> ChannelVolumesPercent { get; } = [];
    }
}

internal sealed record LinuxCaptureStreamInspection(LinuxSourceOutputState? State, string? Failure)
{
    public bool Succeeded => State is not null;
}

internal sealed record LinuxCaptureStreamNormalization(LinuxSourceOutputState? InitialState, string Message);

internal sealed class LinuxCaptureStreamManager
{
    internal const int DefaultDiscoveryAttempts = 12;
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(75);
    private static readonly TimeSpan DefaultDiscoveryTimeout = TimeSpan.FromSeconds(2);
    private readonly IPulseCommandRunner _commandRunner;
    private readonly int _discoveryAttempts;
    private readonly TimeSpan _retryDelay;
    private readonly TimeSpan _discoveryTimeout;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    public LinuxCaptureStreamManager(
        IPulseCommandRunner commandRunner,
        int discoveryAttempts = DefaultDiscoveryAttempts,
        TimeSpan? retryDelay = null,
        TimeSpan? discoveryTimeout = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(discoveryAttempts, 1);
        _commandRunner = commandRunner;
        _discoveryAttempts = discoveryAttempts;
        _retryDelay = retryDelay ?? DefaultRetryDelay;
        _discoveryTimeout = discoveryTimeout ?? DefaultDiscoveryTimeout;
        _delayAsync = delayAsync ?? Task.Delay;
    }

    public async Task<LinuxCaptureStreamNormalization> NormalizeAsync(
        int parecProcessId,
        CancellationToken cancellationToken)
    {
        using var discoveryCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        discoveryCancellation.CancelAfter(_discoveryTimeout);
        var discoveryToken = discoveryCancellation.Token;
        string? lastQueryFailure = null;
        var completedQuery = false;
        try
        {
            for (var attempt = 1; attempt <= _discoveryAttempts; attempt++)
            {
                var inspection = await InspectAsync(parecProcessId, discoveryToken).ConfigureAwait(false);
                if (inspection.State is { } state)
                    return await NormalizeStateAsync(state, discoveryToken).ConfigureAwait(false);

                if (inspection.Failure is { } failure)
                    lastQueryFailure = failure;
                else
                    completedQuery = true;

                if (attempt < _discoveryAttempts)
                    await _delayAsync(_retryDelay, discoveryToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LinuxCaptureStreamNormalization(null,
                $"Could not inspect the app-owned Linux capture stream within " +
                $"{_discoveryTimeout.TotalSeconds:0.#} seconds. " +
                "Capture will continue without stream-state normalization.");
        }

        var detail = !completedQuery && lastQueryFailure is not null
            ? $"pactl list source-outputs failed: {lastQueryFailure}"
            : $"no source-output with application.process.id={parecProcessId} appeared";
        return new LinuxCaptureStreamNormalization(null,
            $"Could not inspect the app-owned Linux capture stream after {_discoveryAttempts} attempts: {detail}. " +
            "Capture will continue without stream-state normalization.");
    }

    public async Task<LinuxCaptureStreamInspection> InspectAsync(
        int parecProcessId,
        CancellationToken cancellationToken)
    {
        PulseCommandResult query;
        try
        {
            query = await _commandRunner.RunAsync(
                cancellationToken, "list", "source-outputs").ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new LinuxCaptureStreamInspection(null, exception.Message);
        }

        if (!query.Succeeded)
            return new LinuxCaptureStreamInspection(null, query.Error);

        var matches = LinuxSourceOutputParser.Parse(query.StandardOutput)
            .Where(sourceOutput => sourceOutput.ProcessId == parecProcessId)
            .ToArray();
        return matches.Length switch
        {
            1 => new LinuxCaptureStreamInspection(matches[0], null),
            > 1 => new LinuxCaptureStreamInspection(null,
                $"multiple source-outputs reported application.process.id={parecProcessId}"),
            _ => new LinuxCaptureStreamInspection(null, null)
        };
    }

    private async Task<LinuxCaptureStreamNormalization> NormalizeStateAsync(
        LinuxSourceOutputState state,
        CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        var corrected = false;
        if (state.IsMuted is true)
        {
            var result = await RunSafelyAsync(cancellationToken,
                "set-source-output-mute", state.Index.ToString(CultureInfo.InvariantCulture), "0").ConfigureAwait(false);
            if (result.Succeeded) corrected = true;
            else failures.Add($"unmute failed: {result.Error}");
        }

        if (state.ChannelVolumesPercent.Count > 0 && !state.HasUnityGain)
        {
            var result = await RunSafelyAsync(cancellationToken,
                "set-source-output-volume", state.Index.ToString(CultureInfo.InvariantCulture),
                "100%", "100%").ConfigureAwait(false);
            if (result.Succeeded) corrected = true;
            else failures.Add($"unity-volume restore failed: {result.Error}");
        }

        var description = state.Describe();
        if (failures.Count > 0)
        {
            return new LinuxCaptureStreamNormalization(state,
                $"Linux capture stream state inspected ({description}), but normalization was incomplete: " +
                $"{string.Join("; ", failures)}. Capture will continue.");
        }

        if (state.IsMuted is null || state.ChannelVolumesPercent.Count == 0)
        {
            return new LinuxCaptureStreamNormalization(state,
                $"Linux capture stream state was only partially available ({description}); capture will continue.");
        }

        return corrected
            ? new LinuxCaptureStreamNormalization(state,
                $"Linux capture stream inherited muted/attenuated state ({description}); " +
                "restored to unmuted unity gain.")
            : new LinuxCaptureStreamNormalization(state,
                $"Linux capture stream state: {description} (unmuted unity gain).");
    }

    private async Task<PulseCommandResult> RunSafelyAsync(
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        try
        {
            return await _commandRunner.RunAsync(cancellationToken, arguments).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return PulseCommandResult.Failure(exception.Message);
        }
    }
}

internal sealed class ExactSilenceDetector(int thresholdFrames)
{
    private long _consecutiveSilentFrames;
    private bool _reported;

    public bool Observe(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (_reported) return false;
        var frames = Math.Min(left.Length, right.Length);
        for (var index = 0; index < frames; index++)
        {
            if (left[index] != 0 || right[index] != 0)
            {
                _consecutiveSilentFrames = 0;
                return false;
            }
        }

        _consecutiveSilentFrames += frames;
        if (_consecutiveSilentFrames < thresholdFrames) return false;
        _reported = true;
        return true;
    }

    public void Reset()
    {
        _consecutiveSilentFrames = 0;
        _reported = false;
    }
}
