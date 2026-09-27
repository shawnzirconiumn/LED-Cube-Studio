namespace LedCube.Core.Visualization.Builder;

public static class VisualizerPresets
{
    public static IReadOnlyList<VisualizerDefinition> CreateAll() => [CreateBeatPulse(), CreateSpectrumColumns(), CreateRain()];

    public static VisualizerDefinition CreateBeatPulse() => new()
    {
        Id = "example-beat-pulse", Name = "Beat Pulse", Description = "A cyan-to-magenta shell driven by the beat envelope.",
        Layers = [new LayerDefinition { Name = "Beat shell", Effect = EffectKind.Pulse,
            Source = new ControlSourceDefinition { Kind = ControlSourceKind.BeatTrigger },
            Spatial = new SpatialDefinition { Thickness = .2 },
            Color = new ColorMappingDefinition { Kind = ColorMappingKind.Gradient,
                Primary = new RgbColorDefinition(0, 220, 255), Secondary = new RgbColorDefinition(255, 0, 180) } }]
    };

    public static VisualizerDefinition CreateSpectrumColumns() => new()
    {
        Id = "example-spectrum-columns", Name = "Spectrum Columns", Description = "FFT bands mapped across rising columns.",
        Layers = [new LayerDefinition { Name = "Spectrum", Effect = EffectKind.SpectrumColumns,
            Source = new ControlSourceDefinition { Kind = ControlSourceKind.Constant, Constant = 1 },
            Spatial = new SpatialDefinition { Axis = Axis.Z, Direction = 1 },
            Color = new ColorMappingDefinition { Kind = ColorMappingKind.Gradient, AudioDrivenIntensity = false,
                Primary = new RgbColorDefinition(0, 255, 80), Secondary = new RgbColorDefinition(255, 40, 0) } }]
    };

    public static VisualizerDefinition CreateRain() => new()
    {
        Id = "example-rain", Name = "Rain", Description = "Audio-reactive falling blue points.",
        Layers = [new LayerDefinition { Name = "Drops", Effect = EffectKind.Rain,
            Source = new ControlSourceDefinition { Kind = ControlSourceKind.OverallLevel },
            Spatial = new SpatialDefinition { Axis = Axis.Z, Direction = -1, Density = .35 },
            Timing = new TimingDefinition { Speed = .75, Lifetime = 1.4 },
            Color = new ColorMappingDefinition { Kind = ColorMappingKind.Gradient,
                Primary = new RgbColorDefinition(20, 80, 255), Secondary = new RgbColorDefinition(150, 240, 255) } }]
    };
}
