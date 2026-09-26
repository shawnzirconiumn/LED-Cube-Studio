using System.Diagnostics;

namespace LedCube.Core.Diagnostics;

/// <summary>One process-wide monotonic timebase for audio-to-serial diagnostics.</summary>
public static class PipelineClock
{
    public static long Frequency => Stopwatch.Frequency;

    public static long GetTimestamp() => Stopwatch.GetTimestamp();

    public static long Add(long timestamp, TimeSpan duration) =>
        timestamp + (long)Math.Round(duration.TotalSeconds * Stopwatch.Frequency);

    public static TimeSpan Elapsed(long startTimestamp, long endTimestamp) =>
        startTimestamp <= 0 || endTimestamp < startTimestamp
            ? TimeSpan.Zero
            : Stopwatch.GetElapsedTime(startTimestamp, endTimestamp);
}
