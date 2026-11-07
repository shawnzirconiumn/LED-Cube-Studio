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
            ControlSourceKind.Constant => 1,
            ControlSourceKind.OverallLevel => audio.RawLevel,
            ControlSourceKind.AudioBand => source.Band < audio.Spectrum.Count ? audio.Spectrum[source.Band] : 0,
            ControlSourceKind.BeatTrigger => audio.IsBeat ? 1 : audio.BeatEnvelope,
            ControlSourceKind.Oscillator => .5 + .5 * Math.Sin(
                elapsed.TotalSeconds * Math.Tau * source.Frequency + source.Phase * Math.Tau),
            _ => 0
        };
        value = Math.Clamp(double.IsFinite(value) ? value : 0, 0, 1);
        if (source.Kind is ControlSourceKind.OverallLevel or ControlSourceKind.AudioBand)
        {
            // Analyzer values are normalized but intentionally conservative. This perceptual curve
            // expands the useful lower range; ReactionStrength is the one user-controlled gain.
            value = Math.Pow(value, .65) * source.ReactionStrength;
        }
        else if (source.Kind == ControlSourceKind.BeatTrigger)
        {
            // Beat hits already peak at 1. A hard gain-and-clamp therefore made every strength at
            // or above 100% identical. A soft knee keeps the hit discrete, preserves its envelope,
            // and leaves useful headroom across the full strength range.
            var amplified = Math.Pow(value, .65) * source.ReactionStrength;
            value = amplified / (1 + amplified);
        }
        return Math.Clamp(double.IsFinite(value) ? value : 0, 0, 1);
    }
}
