namespace LedCube.Core.Diagnostics;

/// <summary>
/// Fixed-size per-second aggregates for recent complete frames. It retains no per-frame timestamp queue.
/// </summary>
public sealed class RollingFrameTimingWindow
{
    private readonly Bucket[] _buckets;
    private long _previousCompletionTimestamp;
    private long _latestCompletionTimestamp;
    private double _latestAudioAgeMilliseconds;

    public RollingFrameTimingWindow(int windowSeconds = 10)
    {
        if (windowSeconds is < 5 or > 60)
            throw new ArgumentOutOfRangeException(nameof(windowSeconds), "Window must be between 5 and 60 seconds.");
        _buckets = new Bucket[windowSeconds];
    }

    public int WindowSeconds => _buckets.Length;

    public void Add(long completionTimestamp, double audioAgeMilliseconds, double sendDurationMilliseconds)
    {
        if (completionTimestamp <= 0 || !double.IsFinite(audioAgeMilliseconds) || audioAgeMilliseconds < 0 ||
            !double.IsFinite(sendDurationMilliseconds) || sendDurationMilliseconds < 0)
            return;

        var second = completionTimestamp / PipelineClock.Frequency;
        var index = (int)(second % _buckets.Length);
        ref var bucket = ref _buckets[index];
        if (bucket.Second != second) bucket = new Bucket { Second = second, MinimumAudioAge = double.MaxValue };

        bucket.FrameCount++;
        if (bucket.FirstCompletionTimestamp == 0) bucket.FirstCompletionTimestamp = completionTimestamp;
        bucket.LastCompletionTimestamp = completionTimestamp;
        bucket.AudioAgeTotal += audioAgeMilliseconds;
        bucket.MinimumAudioAge = Math.Min(bucket.MinimumAudioAge, audioAgeMilliseconds);
        bucket.MaximumAudioAge = Math.Max(bucket.MaximumAudioAge, audioAgeMilliseconds);
        bucket.SendDurationTotal += sendDurationMilliseconds;

        if (_previousCompletionTimestamp > 0 && completionTimestamp >= _previousCompletionTimestamp)
        {
            bucket.CompletionIntervalTotal +=
                PipelineClock.Elapsed(_previousCompletionTimestamp, completionTimestamp).TotalMilliseconds;
            bucket.CompletionIntervalCount++;
        }

        _previousCompletionTimestamp = completionTimestamp;
        _latestCompletionTimestamp = completionTimestamp;
        _latestAudioAgeMilliseconds = audioAgeMilliseconds;
    }

    public RollingFrameTimingSummary GetSummary(long nowTimestamp)
    {
        if (nowTimestamp <= 0) return default;
        var currentSecond = nowTimestamp / PipelineClock.Frequency;
        var oldestSecond = currentSecond - _buckets.Length + 1;
        var count = 0L;
        var ageTotal = 0d;
        var ageMinimum = double.MaxValue;
        var ageMaximum = 0d;
        var sendTotal = 0d;
        var intervalTotal = 0d;
        var intervalCount = 0L;
        var firstCompletionTimestamp = long.MaxValue;
        var lastCompletionTimestamp = 0L;

        foreach (var bucket in _buckets)
        {
            if (bucket.Second < oldestSecond || bucket.Second > currentSecond || bucket.FrameCount == 0) continue;
            count += bucket.FrameCount;
            ageTotal += bucket.AudioAgeTotal;
            ageMinimum = Math.Min(ageMinimum, bucket.MinimumAudioAge);
            ageMaximum = Math.Max(ageMaximum, bucket.MaximumAudioAge);
            sendTotal += bucket.SendDurationTotal;
            intervalTotal += bucket.CompletionIntervalTotal;
            intervalCount += bucket.CompletionIntervalCount;
            firstCompletionTimestamp = Math.Min(firstCompletionTimestamp, bucket.FirstCompletionTimestamp);
            lastCompletionTimestamp = Math.Max(lastCompletionTimestamp, bucket.LastCompletionTimestamp);
        }

        if (count == 0 || _latestCompletionTimestamp / PipelineClock.Frequency < oldestSecond) return default;
        var completionSpanSeconds = count > 1
            ? PipelineClock.Elapsed(firstCompletionTimestamp, lastCompletionTimestamp).TotalSeconds
            : 0;
        return new RollingFrameTimingSummary(
            count,
            _latestAudioAgeMilliseconds,
            ageMinimum,
            ageTotal / count,
            ageMaximum,
            completionSpanSeconds > 0 ? (count - 1) / completionSpanSeconds : 0,
            sendTotal / count,
            intervalCount > 0 ? intervalTotal / intervalCount : 0,
            _buckets.Length);
    }

    public void Reset()
    {
        Array.Clear(_buckets);
        _previousCompletionTimestamp = 0;
        _latestCompletionTimestamp = 0;
        _latestAudioAgeMilliseconds = 0;
    }

    private struct Bucket
    {
        public long Second;
        public long FrameCount;
        public long FirstCompletionTimestamp;
        public long LastCompletionTimestamp;
        public double AudioAgeTotal;
        public double MinimumAudioAge;
        public double MaximumAudioAge;
        public double SendDurationTotal;
        public double CompletionIntervalTotal;
        public long CompletionIntervalCount;
    }
}

public readonly record struct RollingFrameTimingSummary(
    long FrameCount,
    double CurrentAudioAgeMilliseconds,
    double MinimumAudioAgeMilliseconds,
    double AverageAudioAgeMilliseconds,
    double MaximumAudioAgeMilliseconds,
    double CompleteFramesPerSecond,
    double AverageSendDurationMilliseconds,
    double AverageCompletionIntervalMilliseconds,
    int WindowSeconds)
{
    public bool HasSamples => FrameCount > 0;
}
