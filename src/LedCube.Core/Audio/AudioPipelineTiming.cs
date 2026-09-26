namespace LedCube.Core.Audio;

/// <summary>Monotonic timestamps for the audio portion of one analyzed FFT window.</summary>
public readonly record struct AudioPipelineTiming(
    long AudioDataTimestamp,
    long CaptureReceivedTimestamp,
    long AnalyzerSubmittedTimestamp,
    long FftCompletedTimestamp,
    long SnapshotPublishedTimestamp);
