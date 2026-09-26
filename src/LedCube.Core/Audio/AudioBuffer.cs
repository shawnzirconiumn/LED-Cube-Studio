namespace LedCube.Core.Audio;

/// <summary>Normalized floating-point PCM produced by a platform capture adapter.</summary>
public sealed record AudioBuffer(float[] Left, float[] Right, int SampleRate)
{
    public int SampleCount => Math.Min(Left.Length, Right.Length);

    /// <summary>Number of encoded PCM bytes consumed to produce this buffer, when known.</summary>
    public int SourceByteCount { get; init; }

    /// <summary>Monotonic timestamp of the newest sample in this block, when known.</summary>
    public long AudioDataTimestamp { get; init; }

    /// <summary>Monotonic timestamp at which the capture adapter delivered this block.</summary>
    public long CaptureReceivedTimestamp { get; init; }
}
