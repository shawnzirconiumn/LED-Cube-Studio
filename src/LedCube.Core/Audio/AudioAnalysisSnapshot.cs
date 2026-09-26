namespace LedCube.Core.Audio;

public sealed record AudioAnalysisSnapshot(
    double RawLevel,
    double LeftLevel,
    double RightLevel,
    IReadOnlyList<double> Spectrum,
    IReadOnlyList<double> LeftSpectrum,
    IReadOnlyList<double> RightSpectrum,
    double Bass,
    double Mid,
    double Treble,
    bool IsBeat,
    double BeatEnvelope,
    DateTimeOffset Timestamp)
{
    private static readonly double[] EmptyBands = new double[8];

    public static AudioAnalysisSnapshot Silence { get; } = new(
        0, 0, 0, EmptyBands, EmptyBands, EmptyBands, 0, 0, 0, false, 0, DateTimeOffset.MinValue);

    public AudioPipelineTiming PipelineTiming { get; init; }
    public int SensitivityPercent { get; init; } = 100;
}
