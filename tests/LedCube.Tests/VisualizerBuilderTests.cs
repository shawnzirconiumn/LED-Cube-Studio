using LedCube.Core.Audio;
using LedCube.Core.Devices;
using LedCube.Core.Scene;
using LedCube.Core.Visualization;
using LedCube.Core.Visualization.Builder;

namespace LedCube.Tests;

public sealed class VisualizerBuilderTests
{
    [Fact]
    public void DefinitionJsonRoundTripsAndIgnoresUnknownOptionalFields()
    {
        var definition = VisualizerPresets.CreateRain();
        var json = VisualizerDefinitionSerializer.Serialize(definition);
        json = json.Replace("\"description\":", "\"futureEditorHint\": { \"snap\": true }, \"description\":");

        var copy = VisualizerDefinitionSerializer.Deserialize(json);

        Assert.Equal(VisualizerDefinition.CurrentSchemaVersion, copy.SchemaVersion);
        Assert.Equal(definition.Name, copy.Name);
        Assert.Equal(EffectKind.Rain, copy.Layers.Single().Effect);
        Assert.Equal(ControlSourceKind.OverallLevel, copy.Layers.Single().Source.Kind);
    }

    [Fact]
    public void NewerSchemaVersionIsRejectedWithoutCrashingDeserializer()
    {
        var definition = VisualizerPresets.CreateBeatPulse();
        definition.SchemaVersion = VisualizerDefinition.CurrentSchemaVersion + 1;

        var parsed = VisualizerDefinitionSerializer.Deserialize(VisualizerDefinitionSerializer.Serialize(definition));
        var validation = VisualizerDefinitionValidator.Validate(parsed);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("newer than supported", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidationReportsMalformedLayerParameters()
    {
        var definition = VisualizerPresets.CreateRain();
        definition.Name = "";
        definition.Layers[0].Spatial.Direction = 0;
        definition.Layers[0].Color.Brightness = double.NaN;

        var result = VisualizerDefinitionValidator.Validate(definition);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void EnabledLayerOrderControlsOverwriteComposition()
    {
        var red = SolidLayer("red", new RgbColorDefinition(255, 0, 0));
        var blue = SolidLayer("blue", new RgbColorDefinition(0, 0, 255));
        var definition = new VisualizerDefinition { Name = "Order", Layers = [red, blue] };
        var runtime = new CustomVisualizerRuntime(definition);
        var scene = new VoxelScene(3, 4, 5);

        runtime.Render(scene, AudioAnalysisSnapshot.Silence, Frame());
        Assert.Equal(VoxelColor.Blue, scene[1, 2, 3]);

        definition.Layers = [blue, red];
        Assert.True(runtime.TryUpdate(definition));
        runtime.Render(scene, AudioAnalysisSnapshot.Silence, Frame());
        Assert.Equal(VoxelColor.Red, scene[1, 2, 3]);
    }

    [Fact]
    public void InvalidLiveEditPreservesLastCompiledOutput()
    {
        var definition = new VisualizerDefinition { Name = "Stable", Layers = [SolidLayer("red", new RgbColorDefinition(255, 0, 0))] };
        var runtime = new CustomVisualizerRuntime(definition);
        var scene = new VoxelScene(2, 2, 2);
        definition.Layers[0].Color.Primary = new RgbColorDefinition(0, 0, 255);
        definition.Layers[0].Spatial.Thickness = 0;

        Assert.False(runtime.TryUpdate(definition));
        runtime.Render(scene, AudioAnalysisSnapshot.Silence, Frame());
        Assert.Equal(VoxelColor.Red, scene[0, 0, 0]);
    }

    [Fact]
    public void ControlSourcesEvaluateExpectedAudioAndTimeValues()
    {
        var audio = Snapshot(level: .4, bands: [.1, .7], beat: false, beatEnvelope: .8);

        Assert.Equal(.25, ControlSourceEvaluator.Evaluate(new ControlSourceDefinition
            { Kind = ControlSourceKind.Constant, Constant = .25 }, audio, TimeSpan.Zero), 6);
        Assert.Equal(.4, ControlSourceEvaluator.Evaluate(new ControlSourceDefinition
            { Kind = ControlSourceKind.OverallLevel }, audio, TimeSpan.Zero), 6);
        Assert.Equal(.7, ControlSourceEvaluator.Evaluate(new ControlSourceDefinition
            { Kind = ControlSourceKind.AudioBand, Band = 1 }, audio, TimeSpan.Zero), 6);
        Assert.Equal(.8, ControlSourceEvaluator.Evaluate(new ControlSourceDefinition
            { Kind = ControlSourceKind.BeatTrigger }, audio, TimeSpan.Zero), 6);
        Assert.Equal(1, ControlSourceEvaluator.Evaluate(new ControlSourceDefinition
            { Kind = ControlSourceKind.Oscillator, Frequency = 1, Phase = .25 }, audio, TimeSpan.Zero), 6);
    }

    [Fact]
    public void GradientColorMappingInterpolatesAndAdaptsToMonochrome()
    {
        var mapping = new ColorMappingDefinition
        {
            Kind = ColorMappingKind.Gradient, AudioDrivenIntensity = false,
            Primary = new RgbColorDefinition(255, 0, 0), Secondary = new RgbColorDefinition(0, 0, 255)
        };

        var rgb = ColorMapper.Map(mapping, .5, 0, DeviceCapabilities.Rgb);
        var mono = ColorMapper.Map(mapping, .5, 0, DeviceCapabilities.MonochromeIntensity);

        Assert.Equal(new VoxelColor(128, 0, 128, 36), rgb);
        Assert.Equal(mono.R, mono.G);
        Assert.Equal(mono.G, mono.B);
        Assert.Equal(mono.B, mono.Intensity);
    }

    [Theory]
    [InlineData(3, 5, 7)]
    [InlineData(8, 8, 32)]
    [InlineData(16, 16, 16)]
    public void PresetsRenderAcrossArbitraryDimensions(int width, int depth, int height)
    {
        foreach (var definition in VisualizerPresets.CreateAll())
        {
            var runtime = new CustomVisualizerRuntime(definition);
            var scene = new VoxelScene(width, depth, height);
            runtime.Render(scene, Snapshot(.7, [.2, .4, .6, .8, .9, .5, .3, .1], true, 1), Frame());
            Assert.True(scene.CountLitVoxels() > 0, definition.Name);
        }
    }

    private static LayerDefinition SolidLayer(string name, RgbColorDefinition color) => new()
    {
        Name = name, Effect = EffectKind.FullVolume, Blend = BlendMode.Overwrite,
        Source = new ControlSourceDefinition { Kind = ControlSourceKind.Constant, Constant = 1 },
        Color = new ColorMappingDefinition { Primary = color, AudioDrivenIntensity = false }
    };

    private static VisualizationFrame Frame() => new(TimeSpan.FromSeconds(.25), TimeSpan.FromMilliseconds(50), 1, DeviceCapabilities.Rgb);

    private static AudioAnalysisSnapshot Snapshot(double level, IReadOnlyList<double> bands, bool beat, double beatEnvelope) =>
        new(level, level, level, bands, bands, bands, bands.FirstOrDefault(), 0, 0, beat, beatEnvelope, DateTimeOffset.UtcNow);
}
