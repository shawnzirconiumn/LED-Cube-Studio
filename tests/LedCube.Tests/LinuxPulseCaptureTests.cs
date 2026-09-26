using LedCube.Audio;

namespace LedCube.Tests;

public sealed class LinuxPulseCaptureTests
{
    [Fact]
    public void ParecRequestsLowLatencyAndShortProcessingFragments()
    {
        var source = new AudioSourceInfo("test.monitor", "Test monitor", true);
        var arguments = LinuxPulseCapture.CreateStartInfo(source).ArgumentList;

        Assert.Contains("--latency-msec=20", arguments);
        Assert.Contains("--process-time-msec=10", arguments);
        Assert.Contains("--format=float32le", arguments);
        Assert.Contains("--rate=48000", arguments);
        Assert.Contains("--channels=2", arguments);
    }

    [Fact]
    public async Task AlreadyUnmutedAtUnityGainNeedsNoCorrection()
    {
        var runner = new FakePulseCommandRunner(PulseCommandResult.Success(SourceOutput(
            index: 41, processId: 7001, muted: false, leftPercent: 100, rightPercent: 100)));
        var manager = CreateManager(runner);

        var result = await manager.NormalizeAsync(7001, CancellationToken.None);

        Assert.Single(runner.Calls);
        Assert.Contains("mute=no", result.Message);
        Assert.Contains("volume=100%/100%", result.Message);
        Assert.Contains("unmuted unity gain", result.Message);
    }

    [Fact]
    public async Task MutedZeroVolumeStreamIsUnmutedAndBothChannelsAreRestored()
    {
        var runner = new FakePulseCommandRunner(PulseCommandResult.Success(SourceOutput(
            index: 42, processId: 7002, muted: true, leftPercent: 0, rightPercent: 0)));
        var manager = CreateManager(runner);

        var result = await manager.NormalizeAsync(7002, CancellationToken.None);

        Assert.Collection(runner.Calls,
            call => Assert.Equal(["list", "source-outputs"], call),
            call => Assert.Equal(["set-source-output-mute", "42", "0"], call),
            call => Assert.Equal(["set-source-output-volume", "42", "100%", "100%"], call));
        Assert.Contains("inherited muted/attenuated state", result.Message);
        Assert.Contains("restored to unmuted unity gain", result.Message);
    }

    [Fact]
    public async Task AttenuatedUnmutedStreamOnlyNeedsVolumeCorrection()
    {
        var runner = new FakePulseCommandRunner(PulseCommandResult.Success(SourceOutput(
            index: 43, processId: 7003, muted: false, leftPercent: 35, rightPercent: 35)));
        var manager = CreateManager(runner);

        var result = await manager.NormalizeAsync(7003, CancellationToken.None);

        Assert.Collection(runner.Calls,
            call => Assert.Equal(["list", "source-outputs"], call),
            call => Assert.Equal(["set-source-output-volume", "43", "100%", "100%"], call));
        Assert.Contains("volume=35%/35%", result.Message);
        Assert.Contains("restored to unmuted unity gain", result.Message);
    }

    [Fact]
    public async Task ExactParecProcessIdSelectsStreamAmongUnrelatedSourceOutputs()
    {
        var listing = SourceOutput(70, 1234, true, 0, 0, "Other recorder") +
                      SourceOutput(71, 7004, false, 65, 65) +
                      SourceOutput(72, 5678, true, 0, 0, "Another recorder");
        var runner = new FakePulseCommandRunner(PulseCommandResult.Success(listing));
        var manager = CreateManager(runner);

        await manager.NormalizeAsync(7004, CancellationToken.None);

        Assert.Collection(runner.Calls,
            call => Assert.Equal(["list", "source-outputs"], call),
            call => Assert.Equal(["set-source-output-volume", "71", "100%", "100%"], call));
    }

    [Fact]
    public async Task SourceOutputCanAppearAfterShortStartupDelay()
    {
        var runner = new FakePulseCommandRunner(
            PulseCommandResult.Success(string.Empty),
            PulseCommandResult.Success(SourceOutput(80, 9999, false, 100, 100, "Unrelated")),
            PulseCommandResult.Success(SourceOutput(81, 7005, false, 100, 100)));
        var delayCount = 0;
        var manager = new LinuxCaptureStreamManager(
            runner,
            discoveryAttempts: 3,
            retryDelay: TimeSpan.FromMilliseconds(1),
            delayAsync: (_, _) =>
            {
                delayCount++;
                return Task.CompletedTask;
            });

        var result = await manager.NormalizeAsync(7005, CancellationToken.None);

        Assert.Equal(3, runner.Calls.Count);
        Assert.Equal(2, delayCount);
        Assert.NotNull(result.InitialState);
        Assert.Equal(81, result.InitialState.Index);
    }

    [Fact]
    public async Task MissingSourceOutputStopsAfterBoundedAttemptCount()
    {
        var runner = new FakePulseCommandRunner(
            PulseCommandResult.Success(string.Empty),
            PulseCommandResult.Success(SourceOutput(90, 1111, false, 100, 100)),
            PulseCommandResult.Success(string.Empty));
        var delayCount = 0;
        var manager = new LinuxCaptureStreamManager(
            runner,
            discoveryAttempts: 3,
            retryDelay: TimeSpan.FromMilliseconds(1),
            delayAsync: (_, _) =>
            {
                delayCount++;
                return Task.CompletedTask;
            });

        var result = await manager.NormalizeAsync(7006, CancellationToken.None);

        Assert.Equal(3, runner.Calls.Count);
        Assert.Equal(2, delayCount);
        Assert.Null(result.InitialState);
        Assert.Contains("no source-output with application.process.id=7006 appeared", result.Message);
        Assert.Contains("Capture will continue", result.Message);
    }

    [Fact]
    public async Task PactlQueryFailureIsReportedWithoutThrowing()
    {
        var runner = new FakePulseCommandRunner(PulseCommandResult.Failure("connection refused"));
        var manager = CreateManager(runner);

        var result = await manager.NormalizeAsync(7007, CancellationToken.None);

        Assert.Null(result.InitialState);
        Assert.Contains("pactl list source-outputs failed: connection refused", result.Message);
        Assert.Contains("Capture will continue", result.Message);
    }

    [Fact]
    public async Task PactlCorrectionFailureIsReportedWithoutThrowing()
    {
        var runner = new FakePulseCommandRunner(
            _ => PulseCommandResult.Failure("access denied"),
            PulseCommandResult.Success(SourceOutput(44, 7008, true, 0, 0)));
        var manager = CreateManager(runner);

        var result = await manager.NormalizeAsync(7008, CancellationToken.None);

        Assert.Contains("normalization was incomplete", result.Message);
        Assert.Contains("unmute failed: access denied", result.Message);
        Assert.Contains("unity-volume restore failed: access denied", result.Message);
        Assert.Contains("Capture will continue", result.Message);
    }

    [Fact]
    public void ExactSilenceDiagnosticTriggersOnceAfterThresholdAndResetsOnSignal()
    {
        var detector = new ExactSilenceDetector(thresholdFrames: 4);

        Assert.False(detector.Observe([0, 0], [0, 0]));
        Assert.False(detector.Observe([0.1f], [0]));
        Assert.False(detector.Observe([0, 0, 0], [0, 0, 0]));
        Assert.True(detector.Observe([0], [0]));
        Assert.False(detector.Observe([0, 0, 0, 0], [0, 0, 0, 0]));
    }

    private static LinuxCaptureStreamManager CreateManager(FakePulseCommandRunner runner) =>
        new(runner, discoveryAttempts: 1, retryDelay: TimeSpan.Zero,
            delayAsync: (_, _) => Task.CompletedTask);

    private static string SourceOutput(
        int index,
        int processId,
        bool muted,
        double leftPercent,
        double rightPercent,
        string applicationName = "parec") => $$"""
        Source Output #{{index}}
        	Driver: protocol-native.c
        	Sample Specification: float32le 2ch 48000Hz
        	Channel Map: front-left,front-right
        	Mute: {{(muted ? "yes" : "no")}}
        	Volume: front-left: 65536 / {{leftPercent}}% / 0.00 dB,   front-right: 65536 / {{rightPercent}}% / 0.00 dB
        	Properties:
        		application.name = "{{applicationName}}"
        		application.process.id = "{{processId}}"
        		module-stream-restore.id = "source-output-by-application-name:{{applicationName}}"

        """;

    private sealed class FakePulseCommandRunner : IPulseCommandRunner
    {
        private readonly Queue<PulseCommandResult> _queryResults;
        private readonly Func<string[], PulseCommandResult> _nonQueryResult;

        public FakePulseCommandRunner(params PulseCommandResult[] queryResults)
            : this(_ => PulseCommandResult.Success(), queryResults)
        {
        }

        public FakePulseCommandRunner(
            Func<string[], PulseCommandResult> nonQueryResult,
            params PulseCommandResult[] queryResults)
        {
            _queryResults = new Queue<PulseCommandResult>(queryResults);
            _nonQueryResult = nonQueryResult;
        }

        public List<string[]> Calls { get; } = [];

        public Task<PulseCommandResult> RunAsync(
            CancellationToken cancellationToken,
            params string[] arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(arguments);
            var result = arguments is ["list", "source-outputs"]
                ? _queryResults.Dequeue()
                : _nonQueryResult(arguments);
            return Task.FromResult(result);
        }
    }
}
