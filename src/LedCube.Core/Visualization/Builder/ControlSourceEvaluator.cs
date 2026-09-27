using LedCube.Core.Audio;

namespace LedCube.Core.Visualization.Builder;

public static class ControlSourceEvaluator
{
    public static double Evaluate(ControlSourceDefinition source, AudioAnalysisSnapshot audio, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(audio);
        var value = source.Kind switch
        {
            ControlSourceKind.Constant => source.Constant,
            ControlSourceKind.OverallLevel => audio.RawLevel,
            ControlSourceKind.AudioBand => source.Band < audio.Spectrum.Count ? audio.Spectrum[source.Band] : 0,
            ControlSourceKind.BeatTrigger => audio.IsBeat ? 1 : audio.BeatEnvelope,
            ControlSourceKind.Oscillator => .5 + .5 * Math.Sin(
                elapsed.TotalSeconds * Math.Tau * source.Frequency + source.Phase * Math.Tau),
            _ => 0
        };
        return Math.Clamp(double.IsFinite(value) ? value : 0, 0, 1);
    }
}
