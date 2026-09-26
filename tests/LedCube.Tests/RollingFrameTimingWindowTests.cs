using LedCube.Core.Diagnostics;

namespace LedCube.Tests;

public sealed class RollingFrameTimingWindowTests
{
    [Fact]
    public void SummarizesRecentAudioAgeAndCompleteFrameTiming()
    {
        var window = new RollingFrameTimingWindow(10);
        var start = PipelineClock.Frequency * 100;
        window.Add(start, 100, 40);
        window.Add(PipelineClock.Add(start, TimeSpan.FromMilliseconds(50)), 120, 42);
        window.Add(PipelineClock.Add(start, TimeSpan.FromMilliseconds(100)), 140, 44);
        window.Add(PipelineClock.Add(start, TimeSpan.FromMilliseconds(150)), 160, 46);

        var summary = window.GetSummary(PipelineClock.Add(start, TimeSpan.FromSeconds(1)));

        Assert.True(summary.HasSamples);
        Assert.Equal(160, summary.CurrentAudioAgeMilliseconds);
        Assert.Equal(100, summary.MinimumAudioAgeMilliseconds);
        Assert.Equal(130, summary.AverageAudioAgeMilliseconds);
        Assert.Equal(160, summary.MaximumAudioAgeMilliseconds);
        Assert.InRange(summary.CompleteFramesPerSecond, 19.9, 20.1);
        Assert.Equal(43, summary.AverageSendDurationMilliseconds);
        Assert.InRange(summary.AverageCompletionIntervalMilliseconds, 49.9, 50.1);
    }

    [Fact]
    public void ExpiresSamplesOutsideFixedWindowWithoutAFrameQueue()
    {
        var window = new RollingFrameTimingWindow(10);
        var start = PipelineClock.Frequency * 100;
        window.Add(start, 125, 41);

        var summary = window.GetSummary(PipelineClock.Add(start, TimeSpan.FromSeconds(11)));

        Assert.False(summary.HasSamples);
        Assert.Equal(10, window.WindowSeconds);
    }
}
