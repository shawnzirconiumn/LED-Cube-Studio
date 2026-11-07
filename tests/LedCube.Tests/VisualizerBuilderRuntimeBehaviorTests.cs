using LedCube.Core.Audio;
using LedCube.Core.Devices;
using LedCube.Core.Scene;
using LedCube.Core.Visualization;
using LedCube.Core.Visualization.Builder;

namespace LedCube.Tests;

public sealed class VisualizerBuilderRuntimeBehaviorTests
{
    [Fact]
    public void IntensityScalesVoxelOutput()
    {
        var layer = BaseLayer(EffectKind.FullVolume);
        layer.Color.Primary = new RgbColorDefinition(200, 100, 40);
        var runtime = new CustomVisualizerRuntime(Definition(layer));
        var scene = new VoxelScene(2, 2, 2);

        runtime.Render(scene, AudioAnalysisSnapshot.Silence, Frame());
        Assert.Equal((byte)200, scene[0, 0, 0].R);

        layer.Color.Brightness = .25;
        Assert.True(runtime.TryUpdate(Definition(layer)));
        runtime.Render(scene, AudioAnalysisSnapshot.Silence, Frame());
        Assert.Equal((byte)50, scene[0, 0, 0].R);
    }

    [Fact]
    public void VolumeReactionAlwaysFollowsItsSelectedSource()
    {
        var layer = BaseLayer(EffectKind.FullVolume);
        layer.Source.Kind = ControlSourceKind.OverallLevel;
        layer.Color.Primary = new RgbColorDefinition(200, 0, 0);
        layer.Color.AudioDrivenIntensity = false;
        var scene = new VoxelScene(1, 1, 1);

        new CustomVisualizerRuntime(Definition(layer)).Render(scene, Snapshot(.25), Frame());

        Assert.Equal((byte)Math.Round(200 * Math.Pow(.25, .65)), scene[0, 0, 0].R);
    }

    [Fact]
    public void HiddenRedundantValuesDoNotAlterVolumeOrPlane()
    {
        var volume = BaseLayer(EffectKind.FullVolume);
        volume.Source.Constant = .1;
        volume.Color.Primary = new RgbColorDefinition(200, 0, 0);
        var volumeScene = new VoxelScene(1, 1, 1);
        new CustomVisualizerRuntime(Definition(volume)).Render(volumeScene, AudioAnalysisSnapshot.Silence, Frame());
        Assert.Equal((byte)200, volumeScene[0, 0, 0].R);

        var plane = BaseLayer(EffectKind.Plane);
        plane.Spatial.Axis = Axis.X;
        plane.Spatial.Offset = 0;
        plane.Spatial.Thickness = .05;
        plane.Timing.Speed = 0;
        plane.Timing.Phase = .5;
        var planeScene = new VoxelScene(5, 1, 1);
        new CustomVisualizerRuntime(Definition(plane)).Render(planeScene, AudioAnalysisSnapshot.Silence, Frame());
        Assert.True(planeScene[0, 0, 0].IsLit);
        Assert.False(planeScene[2, 0, 0].IsLit);
    }

    [Fact]
    public void PlaneAxisChangesOccupiedCoordinates()
    {
        var layer = BaseLayer(EffectKind.Plane);
        layer.Spatial.Axis = Axis.X;
        layer.Spatial.Offset = .5;
        layer.Spatial.Thickness = .05;
        layer.Timing.Speed = 0;
        var runtime = new CustomVisualizerRuntime(Definition(layer));
        var scene = new VoxelScene(5, 5, 5);

        runtime.Render(scene, AudioAnalysisSnapshot.Silence, Frame());
        Assert.True(scene[2, 0, 0].IsLit);
        Assert.False(scene[0, 0, 2].IsLit);

        layer.Spatial.Axis = Axis.Z;
        Assert.True(runtime.TryUpdate(Definition(layer)));
        runtime.Render(scene, AudioAnalysisSnapshot.Silence, Frame());
        Assert.False(scene[2, 0, 0].IsLit);
        Assert.True(scene[0, 0, 2].IsLit);
    }

    [Fact]
    public void PlaneSpeedAndDirectionChangeTravelPosition()
    {
        var layer = BaseLayer(EffectKind.Plane);
        layer.Spatial.Axis = Axis.X;
        layer.Spatial.Thickness = .05;
        layer.Timing.Speed = .25;
        var forward = new CustomVisualizerRuntime(Definition(layer));
        var forwardScene = new VoxelScene(5, 1, 1);
        forward.Render(forwardScene, AudioAnalysisSnapshot.Silence, Frame(elapsedSeconds: 1));

        layer.Spatial.Direction = -1;
        var reverse = new CustomVisualizerRuntime(Definition(layer));
        var reverseScene = new VoxelScene(5, 1, 1);
        reverse.Render(reverseScene, AudioAnalysisSnapshot.Silence, Frame(elapsedSeconds: 1));

        Assert.True(forwardScene[1, 0, 0].IsLit);
        Assert.True(reverseScene[3, 0, 0].IsLit);
    }

    [Fact]
    public void PlaneStartPositionAndThicknessChangeItsGeometry()
    {
        var layer = BaseLayer(EffectKind.Plane);
        layer.Spatial.Axis = Axis.X;
        layer.Timing.Speed = 0;
        layer.Spatial.Offset = .25;
        layer.Spatial.Thickness = .05;
        var narrow = new VoxelScene(9, 1, 1);
        new CustomVisualizerRuntime(Definition(layer)).Render(narrow, AudioAnalysisSnapshot.Silence, Frame());

        layer.Spatial.Offset = .75;
        layer.Spatial.Thickness = .55;
        var wide = new VoxelScene(9, 1, 1);
        new CustomVisualizerRuntime(Definition(layer)).Render(wide, AudioAnalysisSnapshot.Silence, Frame());

        Assert.True(narrow[2, 0, 0].IsLit);
        Assert.False(narrow[6, 0, 0].IsLit);
        Assert.True(wide[6, 0, 0].IsLit);
        Assert.True(wide.CountLitVoxels() > narrow.CountLitVoxels());
    }

    [Fact]
    public void SpectrumDirectionFlipsColumnGrowth()
    {
        var layer = BaseLayer(EffectKind.SpectrumColumns);
        layer.Spatial.Axis = Axis.Z;
        var audio = Snapshot(.5, [.5, .5, .5, .5, .5, .5, .5, .5]);
        var upward = new VoxelScene(1, 1, 5);
        new CustomVisualizerRuntime(Definition(layer)).Render(upward, audio, Frame());

        layer.Spatial.Direction = -1;
        var downward = new VoxelScene(1, 1, 5);
        new CustomVisualizerRuntime(Definition(layer)).Render(downward, audio, Frame());

        Assert.True(upward[0, 0, 0].IsLit);
        Assert.False(upward[0, 0, 4].IsLit);
        Assert.False(downward[0, 0, 0].IsLit);
        Assert.True(downward[0, 0, 4].IsLit);
    }

    [Fact]
    public void SpectrumAxisChangesTheGrowthDimension()
    {
        var layer = BaseLayer(EffectKind.SpectrumColumns);
        var audio = Snapshot(.5, [.5, .5, .5, .5, .5, .5, .5, .5]);
        layer.Spatial.Axis = Axis.Z;
        var vertical = new VoxelScene(5, 5, 5);
        new CustomVisualizerRuntime(Definition(layer)).Render(vertical, audio, Frame());

        layer.Spatial.Axis = Axis.X;
        var horizontal = new VoxelScene(5, 5, 5);
        new CustomVisualizerRuntime(Definition(layer)).Render(horizontal, audio, Frame());

        Assert.True(vertical[4, 0, 0].IsLit);
        Assert.False(vertical[0, 0, 4].IsLit);
        Assert.False(horizontal[4, 0, 0].IsLit);
        Assert.True(horizontal[0, 0, 4].IsLit);
    }

    [Fact]
    public void ConstantProducesStableVisiblePulseWithSensibleDefaults()
    {
        var layer = BaseLayer(EffectKind.Pulse);
        layer.Spatial.Thickness = .12;
        var first = new VoxelScene(11, 11, 11);
        var second = new VoxelScene(11, 11, 11);
        var runtime = new CustomVisualizerRuntime(Definition(layer));
        runtime.Render(first, AudioAnalysisSnapshot.Silence, Frame(elapsedSeconds: 0));
        runtime.Render(second, Snapshot(1), Frame(elapsedSeconds: 10));

        Assert.True(first.CountLitVoxels() > 0);
        Assert.Equal(LitCoordinates(first), LitCoordinates(second));
    }

    [Fact]
    public void PulseFadeTimeKeepsAReleasedPulseVisible()
    {
        var layer = BaseLayer(EffectKind.Pulse);
        layer.Source.Kind = ControlSourceKind.OverallLevel;
        layer.Spatial.Thickness = .25;
        layer.Color.AudioDrivenIntensity = true;
        layer.Timing.Decay = 1;
        var slow = new CustomVisualizerRuntime(Definition(layer));
        var slowScene = new VoxelScene(8, 8, 8);
        slow.Render(slowScene, Snapshot(1), Frame(deltaSeconds: .05));
        slow.Render(slowScene, Snapshot(0), Frame(elapsedSeconds: .1, deltaSeconds: .1));

        layer.Timing.Decay = 0;
        var immediate = new CustomVisualizerRuntime(Definition(layer));
        var immediateScene = new VoxelScene(8, 8, 8);
        immediate.Render(immediateScene, Snapshot(1), Frame(deltaSeconds: .05));
        immediate.Render(immediateScene, Snapshot(0), Frame(elapsedSeconds: .1, deltaSeconds: .1));

        Assert.True(slowScene.CountLitVoxels() > 0);
        Assert.Equal(0, immediateScene.CountLitVoxels());
    }

    [Fact]
    public void RainDirectionChangesProgression()
    {
        var forwardLayer = RainLayer(lifetime: .01, direction: 1);
        var reverseLayer = RainLayer(lifetime: .01, direction: -1);
        var forward = new VoxelScene(1, 1, 17);
        var reverse = new VoxelScene(1, 1, 17);

        new CustomVisualizerRuntime(Definition(forwardLayer)).Render(forward, AudioAnalysisSnapshot.Silence, Frame(elapsedSeconds: .5));
        new CustomVisualizerRuntime(Definition(reverseLayer)).Render(reverse, AudioAnalysisSnapshot.Silence, Frame(elapsedSeconds: .5));

        Assert.NotEqual(LitZ(forward), LitZ(reverse));
    }

    [Fact]
    public void RainLifetimeControlsTrailPersistenceIndependentlyOfSpeed()
    {
        var shortScene = new VoxelScene(1, 1, 33);
        var longScene = new VoxelScene(1, 1, 33);
        new CustomVisualizerRuntime(Definition(RainLayer(lifetime: .01))).Render(
            shortScene, AudioAnalysisSnapshot.Silence, Frame());
        new CustomVisualizerRuntime(Definition(RainLayer(lifetime: 1))).Render(
            longScene, AudioAnalysisSnapshot.Silence, Frame());

        Assert.True(longScene.CountLitVoxels() > shortScene.CountLitVoxels());
    }

    [Fact]
    public void RainPatternPhaseOffsetsDropPositions()
    {
        var firstLayer = RainLayer(lifetime: .01);
        var shiftedLayer = RainLayer(lifetime: .01);
        shiftedLayer.Timing.Phase = .25;
        var first = new VoxelScene(1, 1, 17);
        var shifted = new VoxelScene(1, 1, 17);

        new CustomVisualizerRuntime(Definition(firstLayer)).Render(first, AudioAnalysisSnapshot.Silence, Frame());
        new CustomVisualizerRuntime(Definition(shiftedLayer)).Render(shifted, AudioAnalysisSnapshot.Silence, Frame());

        Assert.NotEqual(LitZ(first), LitZ(shifted));
    }

    [Fact]
    public void RainMusicSourceControlsSpawnAmountWithoutApplyingGainToColorAgain()
    {
        var layer = RainLayer(lifetime: .1);
        layer.Source.Kind = ControlSourceKind.OverallLevel;
        layer.Color.AudioDrivenIntensity = false;
        var silent = new VoxelScene(4, 4, 8);
        new CustomVisualizerRuntime(Definition(layer)).Render(silent, AudioAnalysisSnapshot.Silence, Frame());

        var active = new VoxelScene(4, 4, 8);
        new CustomVisualizerRuntime(Definition(layer)).Render(active, Snapshot(1), Frame());

        Assert.Equal(0, silent.CountLitVoxels());
        Assert.True(active.CountLitVoxels() > 0);
    }

    [Fact]
    public void RainDensityChangesTheNumberOfDrops()
    {
        var sparseLayer = RainLayer(lifetime: .01);
        sparseLayer.Spatial.Density = .1;
        var denseLayer = RainLayer(lifetime: .01);
        denseLayer.Spatial.Density = 1;
        var sparse = new VoxelScene(8, 8, 8);
        var dense = new VoxelScene(8, 8, 8);

        new CustomVisualizerRuntime(Definition(sparseLayer)).Render(sparse, AudioAnalysisSnapshot.Silence, Frame());
        new CustomVisualizerRuntime(Definition(denseLayer)).Render(dense, AudioAnalysisSnapshot.Silence, Frame());

        Assert.True(dense.CountLitVoxels() > sparse.CountLitVoxels());
    }

    [Fact]
    public void FrequencyBandSelectionUsesTheChosenAnalyzerBand()
    {
        var audio = Snapshot(.5, [.1, .2, .3, .4, .5, .6, .7, .9]);
        var low = ControlSourceEvaluator.Evaluate(
            new ControlSourceDefinition { Kind = ControlSourceKind.AudioBand, Band = 0 }, audio, TimeSpan.Zero);
        var high = ControlSourceEvaluator.Evaluate(
            new ControlSourceDefinition { Kind = ControlSourceKind.AudioBand, Band = 7 }, audio, TimeSpan.Zero);

        Assert.Equal(Math.Pow(.1, .65), low, 6);
        Assert.Equal(Math.Pow(.9, .65), high, 6);
    }

    [Fact]
    public void MusicLoudnessUsesAUsefulRangeAndReactionStrengthScalesOnce()
    {
        var quiet = ControlSourceEvaluator.Evaluate(
            new ControlSourceDefinition { Kind = ControlSourceKind.OverallLevel }, Snapshot(.02), TimeSpan.Zero);
        var loud = ControlSourceEvaluator.Evaluate(
            new ControlSourceDefinition { Kind = ControlSourceKind.OverallLevel }, Snapshot(.8), TimeSpan.Zero);
        var weak = ControlSourceEvaluator.Evaluate(
            new ControlSourceDefinition { Kind = ControlSourceKind.OverallLevel, ReactionStrength = .5 }, Snapshot(.1), TimeSpan.Zero);
        var strong = ControlSourceEvaluator.Evaluate(
            new ControlSourceDefinition { Kind = ControlSourceKind.OverallLevel, ReactionStrength = 2 }, Snapshot(.1), TimeSpan.Zero);

        Assert.InRange(quiet, .05, .15);
        Assert.InRange(loud, .8, .95);
        Assert.Equal(weak * 4, strong, 6);
    }

    [Fact]
    public void WidelySeparatedFrequencyBandsProduceDifferentRuntimeOutput()
    {
        var layer = BaseLayer(EffectKind.FullVolume);
        layer.Source.Kind = ControlSourceKind.AudioBand;
        layer.Source.Band = 0;
        layer.Color.Primary = new RgbColorDefinition(200, 0, 0);
        var runtime = new CustomVisualizerRuntime(Definition(layer));
        var bass = new VoxelScene(1, 1, 1);
        var treble = new VoxelScene(1, 1, 1);
        var audio = Snapshot(.5, [.08, .1, .2, .3, .4, .5, .7, .9]);
        runtime.Render(bass, audio, Frame());

        layer.Source.Band = 7;
        Assert.True(runtime.TryUpdate(Definition(layer)));
        runtime.Render(treble, audio, Frame());

        Assert.True(treble[0, 0, 0].R > bass[0, 0, 0].R * 2);
    }

    [Fact]
    public void AutomaticPulseRateAndPhaseChangeTimeEvaluation()
    {
        var slow = ControlSourceEvaluator.Evaluate(
            new ControlSourceDefinition { Kind = ControlSourceKind.Oscillator, Frequency = 1 },
            AudioAnalysisSnapshot.Silence, TimeSpan.FromSeconds(.125));
        var fast = ControlSourceEvaluator.Evaluate(
            new ControlSourceDefinition { Kind = ControlSourceKind.Oscillator, Frequency = 2 },
            AudioAnalysisSnapshot.Silence, TimeSpan.FromSeconds(.125));
        var shifted = ControlSourceEvaluator.Evaluate(
            new ControlSourceDefinition { Kind = ControlSourceKind.Oscillator, Frequency = 1, Phase = .25 },
            AudioAnalysisSnapshot.Silence, TimeSpan.Zero);

        Assert.NotEqual(slow, fast);
        Assert.Equal(1, fast, 6);
        Assert.Equal(1, shifted, 6);
    }

    [Fact]
    public void BeatSourceUsesDiscreteHitThenEnvelope()
    {
        var source = new ControlSourceDefinition { Kind = ControlSourceKind.BeatTrigger };
        var hit = ControlSourceEvaluator.Evaluate(source, Snapshot(.1, beat: true, beatEnvelope: .2), TimeSpan.Zero);
        var tail = ControlSourceEvaluator.Evaluate(source, Snapshot(.9, beat: false, beatEnvelope: .2), TimeSpan.Zero);

        Assert.Equal(.5, hit, 6);
        var shapedTail = Math.Pow(.2, .65);
        Assert.Equal(shapedTail / (1 + shapedTail), tail, 6);
        Assert.True(hit > tail);
    }

    [Theory]
    [InlineData(.5, 1d / 3)]
    [InlineData(1, .5)]
    [InlineData(2, 2d / 3)]
    [InlineData(3, .75)]
    public void BeatReactionStrengthRetainsHeadroomAtDetectedHits(double strength, double expected)
    {
        var source = new ControlSourceDefinition
        {
            Kind = ControlSourceKind.BeatTrigger,
            ReactionStrength = strength
        };

        var value = ControlSourceEvaluator.Evaluate(
            source, Snapshot(.1, beat: true, beatEnvelope: 1), TimeSpan.Zero);

        Assert.Equal(expected, value, 6);
    }

    [Fact]
    public void BeatReactionStrengthProducesDistinctPulseGeometryAcrossTheFullRange()
    {
        var coordinates = new HashSet<string>();
        foreach (var strength in new[] { .5, 1d, 2d, 3d })
        {
            var layer = BaseLayer(EffectKind.Pulse);
            layer.Source.Kind = ControlSourceKind.BeatTrigger;
            layer.Source.ReactionStrength = strength;
            layer.Spatial.Thickness = .2;
            layer.Timing.Decay = 0;
            var scene = new VoxelScene(8, 8, 8);

            new CustomVisualizerRuntime(Definition(layer)).Render(
                scene, Snapshot(.1, beat: true, beatEnvelope: 1), Frame());

            Assert.True(scene.CountLitVoxels() > 0);
            coordinates.Add(LitCoordinates(scene));
        }

        Assert.Equal(4, coordinates.Count);
    }

    [Fact]
    public void DisabledLayerProducesNoContribution()
    {
        var layer = BaseLayer(EffectKind.FullVolume);
        layer.Enabled = false;
        var scene = new VoxelScene(3, 3, 3);

        new CustomVisualizerRuntime(Definition(layer)).Render(scene, AudioAnalysisSnapshot.Silence, Frame());

        Assert.Equal(0, scene.CountLitVoxels());
    }

    [Theory]
    [InlineData(BlendMode.Max, 100, 40)]
    [InlineData(BlendMode.Additive, 160, 40)]
    [InlineData(BlendMode.Overwrite, 60, 40)]
    public void BlendModesProduceDistinctOverlap(BlendMode blend, int expectedRed, int expectedBlue)
    {
        var lower = BaseLayer(EffectKind.FullVolume);
        lower.Color.Primary = new RgbColorDefinition(100, 0, 0);
        var upper = BaseLayer(EffectKind.FullVolume);
        upper.Color.Primary = new RgbColorDefinition(60, 0, 40);
        upper.Blend = blend;
        var scene = new VoxelScene(1, 1, 1);

        new CustomVisualizerRuntime(new VisualizerDefinition { Name = "Blend", Layers = [lower, upper] })
            .Render(scene, AudioAnalysisSnapshot.Silence, Frame());

        Assert.Equal((byte)expectedRed, scene[0, 0, 0].R);
        Assert.Equal((byte)expectedBlue, scene[0, 0, 0].B);
    }

    [Fact]
    public void GradientUsesBothColorsAcrossTheSelectedAxis()
    {
        var layer = BaseLayer(EffectKind.FullVolume);
        layer.Spatial.Axis = Axis.Z;
        layer.Color.Kind = ColorMappingKind.Gradient;
        layer.Color.Primary = new RgbColorDefinition(255, 0, 0);
        layer.Color.Secondary = new RgbColorDefinition(0, 0, 255);
        var scene = new VoxelScene(1, 1, 3);

        new CustomVisualizerRuntime(Definition(layer)).Render(scene, AudioAnalysisSnapshot.Silence, Frame());

        Assert.Equal(VoxelColor.Red, scene[0, 0, 0]);
        Assert.Equal(VoxelColor.Blue, scene[0, 0, 2]);
    }

    [Fact]
    public void RainbowProducesDistinctSpatialColorsAndAnimatesDeterministically()
    {
        var layer = BaseLayer(EffectKind.FullVolume);
        layer.Spatial.Axis = Axis.Z;
        layer.Color.Kind = ColorMappingKind.Rainbow;
        layer.Color.CycleOffset = .1;
        layer.Color.CycleSpeed = .25;
        var runtime = new CustomVisualizerRuntime(Definition(layer));
        var first = new VoxelScene(1, 1, 8);
        var again = new VoxelScene(1, 1, 8);
        var later = new VoxelScene(1, 1, 8);
        var offset = new VoxelScene(1, 1, 8);

        runtime.Render(first, AudioAnalysisSnapshot.Silence, Frame(elapsedSeconds: 2));
        runtime.Render(again, AudioAnalysisSnapshot.Silence, Frame(elapsedSeconds: 2));
        runtime.Render(later, AudioAnalysisSnapshot.Silence, Frame(elapsedSeconds: 3));
        layer.Color.CycleOffset = .35;
        layer.Color.CycleSpeed = 0;
        Assert.True(runtime.TryUpdate(Definition(layer)));
        runtime.Render(offset, AudioAnalysisSnapshot.Silence, Frame(elapsedSeconds: 2));

        Assert.True(Enumerable.Range(0, 8).Select(z => first[0, 0, z]).Distinct().Count() >= 6);
        Assert.Equal(Enumerable.Range(0, 8).Select(z => first[0, 0, z]),
            Enumerable.Range(0, 8).Select(z => again[0, 0, z]));
        Assert.NotEqual(first[0, 0, 0], later[0, 0, 0]);
        Assert.NotEqual(first[0, 0, 0], offset[0, 0, 0]);
    }

    [Fact]
    public void AudioDrivenColorOptionChangesSourceDimming()
    {
        var mapping = new ColorMappingDefinition
        {
            Primary = new RgbColorDefinition(200, 0, 0), Brightness = 1, AudioDrivenIntensity = false
        };
        var steady = ColorMapper.Map(mapping, 0, .5, DeviceCapabilities.Rgb);
        mapping.AudioDrivenIntensity = true;
        var reactive = ColorMapper.Map(mapping, 0, .5, DeviceCapabilities.Rgb);

        Assert.Equal((byte)200, steady.R);
        Assert.Equal((byte)100, reactive.R);
    }

    private static VisualizerDefinition Definition(LayerDefinition layer) =>
        new() { Name = "Behavior", Layers = [layer] };

    private static LayerDefinition BaseLayer(EffectKind effect) => new()
    {
        Name = effect.ToString(), Effect = effect,
        Source = new ControlSourceDefinition { Kind = ControlSourceKind.Constant, Constant = 1 },
        Color = new ColorMappingDefinition
        {
            Primary = new RgbColorDefinition(255, 255, 255), AudioDrivenIntensity = false
        }
    };

    private static LayerDefinition RainLayer(double lifetime, int direction = 1)
    {
        var layer = BaseLayer(EffectKind.Rain);
        layer.Spatial.Axis = Axis.Z;
        layer.Spatial.Direction = direction;
        layer.Spatial.Density = 1;
        layer.Timing.Speed = .5;
        layer.Timing.Lifetime = lifetime;
        return layer;
    }

    private static string LitZ(VoxelScene scene) =>
        string.Join(',', Enumerable.Range(0, scene.Height).Where(z => scene[0, 0, z].IsLit));

    private static string LitCoordinates(VoxelScene scene)
    {
        var coordinates = new List<string>();
        for (var z = 0; z < scene.Height; z++)
        for (var y = 0; y < scene.Depth; y++)
        for (var x = 0; x < scene.Width; x++)
            if (scene[x, y, z].IsLit) coordinates.Add($"{x},{y},{z}");
        return string.Join(';', coordinates);
    }

    private static VisualizationFrame Frame(double elapsedSeconds = .25, double deltaSeconds = .05) =>
        new(TimeSpan.FromSeconds(elapsedSeconds), TimeSpan.FromSeconds(deltaSeconds), 1, DeviceCapabilities.Rgb);

    private static AudioAnalysisSnapshot Snapshot(double level, IReadOnlyList<double>? bands = null,
        bool beat = false, double beatEnvelope = 0)
    {
        bands ??= new double[8];
        return new AudioAnalysisSnapshot(level, level, level, bands, bands, bands,
            bands[0], bands.Skip(2).Take(3).DefaultIfEmpty().Average(), bands.Skip(5).Take(3).DefaultIfEmpty().Average(),
            beat, beatEnvelope, DateTimeOffset.UtcNow);
    }
}
