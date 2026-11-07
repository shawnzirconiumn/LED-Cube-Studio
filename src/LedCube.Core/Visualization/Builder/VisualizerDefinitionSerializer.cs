using System.Text.Json;
using System.Text.Json.Serialization;

namespace LedCube.Core.Visualization.Builder;

public static class VisualizerDefinitionSerializer
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(VisualizerDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return JsonSerializer.Serialize(definition, Options);
    }

    public static VisualizerDefinition Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new VisualizerDefinitionException("The visualizer file is empty.");
        try
        {
            var definition = JsonSerializer.Deserialize<VisualizerDefinition>(json, Options)
                             ?? throw new VisualizerDefinitionException("The visualizer file contains no definition.");
            if (definition.SchemaVersion == 1)
            {
                definition.SchemaVersion = VisualizerDefinition.CurrentSchemaVersion;
                foreach (var layer in definition.Layers ?? [])
                {
                    // Version 2 retains fixed/gradient colors and adds neutral reaction/rainbow defaults.
                    if (layer?.Source is not null) layer.Source.ReactionStrength = 1;
                    if (layer?.Color is not null)
                    {
                        layer.Color.CycleOffset = 0;
                        layer.Color.CycleSpeed = 0;
                    }
                }
            }
            return definition;
        }
        catch (JsonException exception)
        {
            throw new VisualizerDefinitionException("The visualizer JSON is malformed.", exception);
        }
    }
}

public sealed class VisualizerDefinitionException : Exception
{
    public VisualizerDefinitionException(string message) : base(message) { }
    public VisualizerDefinitionException(string message, Exception innerException) : base(message, innerException) { }
}
