using System.Text.Json.Serialization;

namespace LedCube.Core.Visualization.Builder;

public sealed class VisualizerDefinition
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled Visualizer";
    public string Description { get; set; } = string.Empty;
    public List<LayerDefinition> Layers { get; set; } = [];
}

public sealed class LayerDefinition
{
    public string Name { get; set; } = "Layer";
    public bool Enabled { get; set; } = true;
    public EffectKind Effect { get; set; } = EffectKind.FullVolume;
    public ControlSourceDefinition Source { get; set; } = new();
    public SpatialDefinition Spatial { get; set; } = new();
    public ColorMappingDefinition Color { get; set; } = new();
    public TimingDefinition Timing { get; set; } = new();
    public BlendMode Blend { get; set; } = BlendMode.Max;
}

public sealed class ControlSourceDefinition
{
    public ControlSourceKind Kind { get; set; } = ControlSourceKind.OverallLevel;
    public double Constant { get; set; } = 1;
    public int Band { get; set; }
    public double Frequency { get; set; } = 1;
    public double Phase { get; set; }
    public double ReactionStrength { get; set; } = 1;
}

public sealed class SpatialDefinition
{
    public Axis Axis { get; set; } = Axis.Z;
    public int Direction { get; set; } = 1;
    public double Offset { get; set; }
    public double Thickness { get; set; } = .15;
    public double Density { get; set; } = .15;
}

public sealed class ColorMappingDefinition
{
    public RgbColorDefinition Primary { get; set; } = new(0, 180, 255);
    public RgbColorDefinition Secondary { get; set; } = new(255, 0, 180);
    public ColorMappingKind Kind { get; set; } = ColorMappingKind.Fixed;
    public bool AudioDrivenIntensity { get; set; } = true;
    public double Brightness { get; set; } = 1;
    public double CycleOffset { get; set; }
    public double CycleSpeed { get; set; }
}

public sealed class TimingDefinition
{
    public double Speed { get; set; } = 1;
    public double Phase { get; set; }
    public double Decay { get; set; } = .8;
    public double Lifetime { get; set; } = 1;
}

public sealed record RgbColorDefinition
{
    public RgbColorDefinition() { }
    public RgbColorDefinition(byte red, byte green, byte blue) => (Red, Green, Blue) = (red, green, blue);
    public byte Red { get; set; }
    public byte Green { get; set; }
    public byte Blue { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EffectKind { FullVolume, Plane, SpectrumColumns, Pulse, Rain }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ControlSourceKind { Constant, OverallLevel, AudioBand, BeatTrigger, Oscillator }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ColorMappingKind { Fixed, Gradient, Rainbow }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BlendMode { Overwrite, Max, Additive }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Axis { X, Y, Z }
