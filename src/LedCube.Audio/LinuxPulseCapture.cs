using System.Diagnostics;
using LedCube.Core.Audio;
using LedCube.Core.Diagnostics;

namespace LedCube.Audio;

/// <summary>PipeWire/PulseAudio-compatible capture through the standard pactl/parec tools.</summary>
internal sealed class LinuxPulseCaptureFactory : IAudioCaptureFactory
{
    public async Task<IReadOnlyList<AudioSourceInfo>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        var sourcesTask = TryReadPactlAsync(cancellationToken, "list", "short", "sources");
        var defaultSinkTask = TryReadPactlAsync(cancellationToken, "get-default-sink");
        await Task.WhenAll(sourcesTask, defaultSinkTask).ConfigureAwait(false);
        return LinuxPulseDiscovery.ParseSources(
            await sourcesTask.ConfigureAwait(false),
            (await defaultSinkTask.ConfigureAwait(false))?.Trim());
    }

    public IAudioCapture Create(AudioSourceInfo source) => new LinuxPulseCapture(source);

    private static Process StartProcess(string fileName, params string[] arguments)
    {
        var info = new ProcessStartInfo(fileName) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return Process.Start(info) ?? throw new InvalidOperationException($"Could not start {fileName}.");
    }

    private static async Task<string?> TryReadPactlAsync(
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        try
        {
            using var process = StartProcess("pactl", arguments);
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0 ? output : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

internal static class LinuxPulseDiscovery
{
    public static IReadOnlyList<AudioSourceInfo> ParseSources(string? sourceList, string? defaultSink)
    {
        var sources = new List<AudioSourceInfo>();
        var defaultMonitor = string.IsNullOrWhiteSpace(defaultSink) ? null : $"{defaultSink.Trim()}.monitor";
        foreach (var line in (sourceList ?? string.Empty).Split(
                     '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = line.Split('\t');
            if (fields.Length < 2) continue;
            var id = fields[1];
            var isMonitor = id.EndsWith(".monitor", StringComparison.OrdinalIgnoreCase);
            var isDefault = string.Equals(id, defaultMonitor, StringComparison.Ordinal);
            sources.Add(new AudioSourceInfo(
                id,
                $"{id} ({(isMonitor ? "output monitor" : "input")}{(isDefault ? ", default" : string.Empty)})",
                isMonitor,
                isDefault));
        }

        if (!sources.Any(source => source.IsOutputMonitor))
        {
            sources.Add(new AudioSourceInfo(
                "@DEFAULT_MONITOR@",
                "Default system output (Pulse/PipeWire monitor)",
                IsOutputMonitor: true,
                IsSystemDefault: true));
        }

        return sources.OrderByDescending(source => source.IsSystemDefault)
            .ThenByDescending(source => source.IsOutputMonitor)
            .ThenBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

internal sealed class LinuxPulseCapture : IAudioCapture
{
    private const int SampleRate = 48000;
    private const int RequestedLatencyMilliseconds = 20;
    private const int RequestedProcessTimeMilliseconds = 10;
    private const int SustainedSilenceSeconds = 3;
    private readonly AudioSourceInfo _source;
    private readonly LinuxCaptureStreamManager _streamManager;
    private readonly ExactSilenceDetector _silenceDetector = new(SampleRate * SustainedSilenceSeconds);
    private Process? _process;
    private CancellationTokenSource? _stop;
    private Task? _readTask;
    private Task? _silenceDiagnosticTask;
    private long _captureStartedTimestamp;
    private long _sampleFramesDelivered;

    public LinuxPulseCapture(AudioSourceInfo source)
        : this(source, new LinuxCaptureStreamManager(new PactlCommandRunner()))
    {
    }

    internal LinuxPulseCapture(AudioSourceInfo source, LinuxCaptureStreamManager streamManager)
    {
        _source = source;
        _streamManager = streamManager;
    }

    public event EventHandler<AudioBuffer>? DataAvailable;
    public event EventHandler<string>? StatusChanged;
    public bool IsRunning => _process is { HasExited: false };

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsRunning) return;
        var info = CreateStartInfo(_source);
        _captureStartedTimestamp = PipelineClock.GetTimestamp();
        _sampleFramesDelivered = 0;
        _silenceDetector.Reset();
        var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start parec. Is pulseaudio-utils installed?");
        var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _process = process;
        _stop = stop;
        _readTask = ReadLoopAsync(process, stop.Token);
        ReportStatus($"Capturing {_source.DisplayName} at {SampleRate} Hz stereo " +
                     $"({RequestedLatencyMilliseconds} ms requested latency); parec PID {process.Id}.");

        try
        {
            var normalization = await _streamManager.NormalizeAsync(process.Id, stop.Token).ConfigureAwait(false);
            ReportStatus(normalization.Message);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ReportStatus($"Could not inspect or normalize the app-owned Linux capture stream for parec PID " +
                         $"{process.Id}: {exception.Message}. Capture will continue.");
        }
    }

    internal static ProcessStartInfo CreateStartInfo(AudioSourceInfo audioSource)
    {
        var info = new ProcessStartInfo("parec")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
                 {
                     "--raw", "--format=float32le", $"--rate={SampleRate}", "--channels=2",
                     $"--device={audioSource.Id}", $"--latency-msec={RequestedLatencyMilliseconds}",
                     $"--process-time-msec={RequestedProcessTimeMilliseconds}"
                 })
            info.ArgumentList.Add(argument);
        return info;
    }

    private async Task ReadLoopAsync(Process process, CancellationToken cancellationToken)
    {
        var stream = process.StandardOutput.BaseStream;
        var buffer = new byte[8192];
        var carry = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(carry), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                var available = carry + read;
                var complete = available - available % 8;
                if (complete > 0)
                {
                    var receivedTimestamp = PipelineClock.GetTimestamp();
                    var sampleFrames = complete / 8;
                    _sampleFramesDelivered += sampleFrames;
                    var audioDataTimestamp = PipelineClock.Add(_captureStartedTimestamp,
                        TimeSpan.FromSeconds(_sampleFramesDelivered / (double)SampleRate));
                    var decoded = PcmDecoder.Decode(buffer, complete, SampleRate, 2, 32, true) with
                    {
                        AudioDataTimestamp = audioDataTimestamp,
                        CaptureReceivedTimestamp = receivedTimestamp
                    };
                    DataAvailable?.Invoke(this, decoded);
                    if (_silenceDetector.Observe(decoded.Left, decoded.Right))
                        _silenceDiagnosticTask = DiagnoseSustainedSilenceAsync(process.Id, cancellationToken);
                }
                carry = available - complete;
                if (carry > 0) Buffer.BlockCopy(buffer, complete, buffer, 0, carry);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ReportStatus($"Audio capture failed: {exception.Message}");
        }
    }

    private async Task DiagnoseSustainedSilenceAsync(int parecProcessId, CancellationToken cancellationToken)
    {
        try
        {
            var inspection = await _streamManager.InspectAsync(parecProcessId, cancellationToken).ConfigureAwait(false);
            if (inspection.State is { } state)
            {
                var interpretation = state.IsMuted is true ||
                                     state.ChannelVolumesPercent.Count > 0 && !state.HasUnityGain
                    ? "The app-owned capture stream is currently muted or attenuated."
                    : state.IsMuted is false && state.HasUnityGain
                        ? "The app-owned capture stream is still unmuted at unity gain; the exact-zero PCM is upstream silence."
                        : "The stream state is incomplete, so the source of the exact-zero PCM is not conclusive.";
                ReportStatus($"Linux capture PCM remained exactly zero for {SustainedSilenceSeconds} seconds. " +
                             $"Current {state.Describe()}. {interpretation}");
            }
            else
            {
                var reason = inspection.Failure ?? $"no source-output matched parec PID {parecProcessId}";
                ReportStatus($"Linux capture PCM remained exactly zero for {SustainedSilenceSeconds} seconds, " +
                             $"but its source-output state could not be re-inspected: {reason}.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ReportStatus($"Linux capture PCM remained exactly zero for {SustainedSilenceSeconds} seconds, " +
                         $"but its source-output state check failed: {exception.Message}.");
        }
    }

    private void ReportStatus(string message)
    {
        Trace.WriteLine($"[linux-audio] {message}");
        StatusChanged?.Invoke(this, message);
    }

    public async Task StopAsync()
    {
        var stop = Interlocked.Exchange(ref _stop, null);
        stop?.Cancel();
        var process = Interlocked.Exchange(ref _process, null);
        if (process is not null)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            try { await process.WaitForExitAsync().ConfigureAwait(false); } catch (InvalidOperationException) { }
            process.Dispose();
        }
        var readTask = Interlocked.Exchange(ref _readTask, null);
        if (readTask is not null)
        {
            try { await readTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }
        var silenceDiagnosticTask = Interlocked.Exchange(ref _silenceDiagnosticTask, null);
        if (silenceDiagnosticTask is not null)
        {
            try { await silenceDiagnosticTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }
        stop?.Dispose();
        ReportStatus("Audio capture stopped.");
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
