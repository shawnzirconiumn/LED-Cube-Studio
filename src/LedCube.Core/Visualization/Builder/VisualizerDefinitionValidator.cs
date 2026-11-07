using LedCube.Core.Audio;

namespace LedCube.Core.Visualization.Builder;

public sealed record DefinitionValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public static class VisualizerDefinitionValidator
{
    public static DefinitionValidationResult Validate(VisualizerDefinition? definition)
    {
        var errors = new List<string>();
        if (definition is null) return new DefinitionValidationResult(["A definition is required."]);
        if (definition.SchemaVersion < 1) errors.Add("Schema version must be at least 1.");
        if (definition.SchemaVersion > VisualizerDefinition.CurrentSchemaVersion)
            errors.Add($"Schema version {definition.SchemaVersion} is newer than supported version {VisualizerDefinition.CurrentSchemaVersion}.");
        if (string.IsNullOrWhiteSpace(definition.Id)) errors.Add("An id is required.");
        if (string.IsNullOrWhiteSpace(definition.Name)) errors.Add("A name is required.");
        if (definition.Layers is null) errors.Add("Layers are required.");
        else for (var index = 0; index < definition.Layers.Count; index++)
            ValidateLayer(definition.Layers[index], index, errors);
        return new DefinitionValidationResult(errors);
    }

    public static void ThrowIfInvalid(VisualizerDefinition definition)
    {
        var result = Validate(definition);
        if (!result.IsValid) throw new VisualizerDefinitionException(string.Join(" ", result.Errors));
    }

    private static void ValidateLayer(LayerDefinition? layer, int index, ICollection<string> errors)
    {
        var prefix = $"Layer {index + 1}";
        if (layer is null) { errors.Add($"{prefix} is missing."); return; }
        if (string.IsNullOrWhiteSpace(layer.Name)) errors.Add($"{prefix} needs a name.");
        if (layer.Source is null || layer.Spatial is null || layer.Color is null || layer.Timing is null)
        {
            errors.Add($"{prefix} has an incomplete source, spatial, color, or timing section.");
            return;
        }
        if (layer.Source.Band is < 0 or >= SpectrumAnalyzer.BandCount)
            errors.Add($"{prefix} band must be between 0 and {SpectrumAnalyzer.BandCount - 1}.");
        if (!InRange(layer.Source.Constant, 0, 1)) errors.Add($"{prefix} constant must be between 0 and 1.");
        if (!InRange(layer.Source.ReactionStrength, .25, 3)) errors.Add($"{prefix} reaction strength must be between 0.25 and 3.");
        if (!InRange(layer.Source.Frequency, 0, 100)) errors.Add($"{prefix} oscillator frequency must be between 0 and 100.");
        if (layer.Spatial.Direction is not (-1 or 1)) errors.Add($"{prefix} direction must be -1 or 1.");
        if (!InRange(layer.Spatial.Offset, 0, 1)) errors.Add($"{prefix} offset must be between 0 and 1.");
        if (!InRange(layer.Spatial.Thickness, .001, 1)) errors.Add($"{prefix} thickness must be greater than 0 and at most 1.");
        if (!InRange(layer.Spatial.Density, 0, 1)) errors.Add($"{prefix} density must be between 0 and 1.");
        if (!InRange(layer.Color.Brightness, 0, 1)) errors.Add($"{prefix} brightness must be between 0 and 1.");
        if (!InRange(layer.Color.CycleOffset, 0, 1)) errors.Add($"{prefix} color cycle offset must be between 0 and 1.");
        if (!InRange(layer.Color.CycleSpeed, -5, 5)) errors.Add($"{prefix} color cycle speed must be between -5 and 5.");
        if (!InRange(layer.Timing.Speed, 0, 100)) errors.Add($"{prefix} speed must be between 0 and 100.");
        if (!InRange(layer.Timing.Decay, 0, 1)) errors.Add($"{prefix} decay must be between 0 and 1.");
        if (!InRange(layer.Timing.Lifetime, .001, 120)) errors.Add($"{prefix} lifetime must be greater than 0 and at most 120 seconds.");
    }

    private static bool InRange(double value, double minimum, double maximum) =>
        double.IsFinite(value) && value >= minimum && value <= maximum;
}
