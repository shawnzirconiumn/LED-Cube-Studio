using LedCube.Core.Audio;
using LedCube.Core.Diagnostics;

namespace LedCube.Tests;

public sealed class SpectrumAnalyzerTests
{
    [Fact]
    public void BinCenteredToneAppearsInExpectedLogBand()
    {
        const int sampleRate = 48000;
        const double frequency = 468.75; // FFT bin 20, inside the 400-800 Hz band.
        var analyzer = new SpectrumAnalyzer { SensitivityPercent = 100, DecayPercent = 15 };
        analyzer.Process(CreateSine(sampleRate, frequency, 1, 4096));

        var spectrum = analyzer.Latest.Spectrum.ToArray();
        Assert.Equal(3, Array.IndexOf(spectrum, spectrum.Max()));
        Assert.True(spectrum[3] > 0.8);
    }

    [Fact]
    public void TenfoldAmplitudeReductionIsApproximatelyTwentyDb()
    {
        const int sampleRate = 48000;
        const double frequency = 468.75;
        var full = new SpectrumAnalyzer();
        var tenth = new SpectrumAnalyzer();
        // Keep both tones below the display's -12 dB saturation point.
        full.Process(CreateSine(sampleRate, frequency, 0.1, 4096));
        tenth.Process(CreateSine(sampleRate, frequency, 0.01, 4096));

        var normalizedDifference = full.Latest.Spectrum[3] - tenth.Latest.Spectrum[3];
        Assert.InRange(normalizedDifference, 0.28, 0.39); // 20 dB across a 60 dB display range.
    }

    [Fact]
    public void PublishesOneSnapshotForEveryCompletedOverlappingFftWindow()
    {
        const int sampleRate = 48000;
        var analyzer = new SpectrumAnalyzer();
        var windowCount = 0;
        analyzer.SnapshotAvailable += (_, _) => windowCount++;

        analyzer.Process(CreateSine(sampleRate, 468.75, 0.5, 4096));

        Assert.Equal(3, windowCount); // 2048 initial samples, then two 1024-sample hops.
    }

    [Fact]
    public void CarriesMonotonicCaptureAndAnalysisTimestampsIntoSnapshots()
    {
        const int sampleRate = 48000;
        var analyzer = new SpectrumAnalyzer();
        var received = PipelineClock.GetTimestamp();
        var buffer = CreateSine(sampleRate, 468.75, .5, 4096) with
        {
            AudioDataTimestamp = PipelineClock.Add(received, TimeSpan.FromMilliseconds(-20)),
            CaptureReceivedTimestamp = received
        };

        analyzer.Process(buffer);

        var timing = analyzer.Latest.PipelineTiming;
        Assert.True(timing.AudioDataTimestamp > 0);
        Assert.Equal(received, timing.CaptureReceivedTimestamp);
        Assert.True(timing.AnalyzerSubmittedTimestamp >= timing.CaptureReceivedTimestamp);
        Assert.True(timing.FftCompletedTimestamp >= timing.AnalyzerSubmittedTimestamp);
        Assert.True(timing.SnapshotPublishedTimestamp >= timing.FftCompletedTimestamp);
    }

    [Fact]
    public void EachPublishedSnapshotRetainsItsOwnRepresentedAudioTimestamp()
    {
        const int sampleRate = 48000;
        var analyzer = new SpectrumAnalyzer();
        var snapshots = new List<AudioAnalysisSnapshot>();
        analyzer.SnapshotAvailable += (_, snapshot) => snapshots.Add(snapshot);
        var received = PipelineClock.GetTimestamp();
        analyzer.Process(CreateSine(sampleRate, 468.75, .5, 4096) with
        {
            AudioDataTimestamp = received,
            CaptureReceivedTimestamp = received
        });

        Assert.Equal(3, snapshots.Count);
        Assert.True(snapshots[1].PipelineTiming.AudioDataTimestamp >
                    snapshots[0].PipelineTiming.AudioDataTimestamp);
        Assert.True(snapshots[2].PipelineTiming.AudioDataTimestamp >
                    snapshots[1].PipelineTiming.AudioDataTimestamp);
        Assert.Equal(snapshots[^1].PipelineTiming, analyzer.Latest.PipelineTiming);
    }

    [Theory]
    [InlineData(0, 1500, 4983)]
    [InlineData(25, 709, 2355)]
    [InlineData(50, 335, 1114)]
    [InlineData(75, 159, 528)]
    [InlineData(100, 75, 249)]
    public void DecaySliderMapsToMeaningfullySpacedReleaseTimes(
        int decayPercent, double expectedHalfMilliseconds, double expectedTenthMilliseconds)
    {
        var halfLife = SpectrumAnalyzer.ReleaseHalfLife(decayPercent);
        var tenthTime = halfLife.TotalMilliseconds * Math.Log(0.1) / Math.Log(0.5);

        Assert.InRange(halfLife.TotalMilliseconds,
            expectedHalfMilliseconds - 1, expectedHalfMilliseconds + 1);
        Assert.InRange(tenthTime, expectedTenthMilliseconds - 2, expectedTenthMilliseconds + 2);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(47)]
    [InlineData(100)]
    public void ReleaseCoefficientIsUpdateRateIndependent(int updatesPerSecond)
    {
        const int decayPercent = 50;
        var elapsed = TimeSpan.FromSeconds(1d / updatesPerSecond);
        var coefficient = SpectrumAnalyzer.ReleaseCoefficient(decayPercent, elapsed);
        var value = 1d;
        for (var update = 0; update < updatesPerSecond; update++)
            value += (0 - value) * coefficient;

        var expected = Math.Pow(.5,
            TimeSpan.FromSeconds(1).TotalSeconds /
            SpectrumAnalyzer.ReleaseHalfLife(decayPercent).TotalSeconds);
        Assert.InRange(value, expected - .000005, expected + .000005);
    }

    private static AudioBuffer CreateSine(int sampleRate, double frequency, double amplitude, int count)
    {
        var samples = Enumerable.Range(0, count)
            .Select(i => (float)(amplitude * Math.Sin(2 * Math.PI * frequency * i / sampleRate))).ToArray();
        return new AudioBuffer(samples, (float[])samples.Clone(), sampleRate);
    }
}
