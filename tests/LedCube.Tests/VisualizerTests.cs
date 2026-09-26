using LedCube.Core.Audio;
using LedCube.Core.Devices;
using LedCube.Core.Scene;
using LedCube.Core.Visualization;

namespace LedCube.Tests;

public sealed class VisualizerTests
{
    private static readonly double[] ActiveBands = [.86, .72, .61, .52, .43, .34, .27, .2];

    [Fact]
    public void RegistryExposesUniqueStableIdsAndFriendlyMetadata()
    {
        var visualizers = VisualizerRegistry.CreateAll();

        Assert.Equal(31, visualizers.Count);
        Assert.Equal(visualizers.Count, visualizers.Select(item => item.Id).Distinct().Count());
        Assert.Equal(visualizers.Count, visualizers.Select(item => item.DisplayName).Distinct().Count());
        Assert.All(visualizers, visualizer =>
        {
            Assert.Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$", visualizer.Id);
            Assert.False(string.IsNullOrWhiteSpace(visualizer.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(visualizer.Description));
            Assert.Null(visualizer.RequiredCapability);
        });

        Assert.Equal("8-Band Spectrum", visualizers.Single(item => item.Id == "eight-band-spectrum").DisplayName);
        Assert.Equal("Bass/Mid/Treble Space", visualizers.Single(item => item.Id == "bass-mid-treble-space").DisplayName);
    }

    [Fact]
    public void RegistryContainsKeeperSetAndExcludesRejectedModes()
    {
        var ids = VisualizerRegistry.CreateAll().Select(item => item.Id).ToHashSet();
        string[] keepers =
        [
            "eight-band-spectrum", "bass-mid-treble-space", "spectrum-waterfall", "bass-shockwave",
            "eq-city", "spectral-rain", "spectral-plasma", "beat-pulse-detail",
            "frequency-firestorm", "rgb-supernova", "beat-fireworks", "spectral-crossfire",
            "frequency-storm", "beat-comets", "rgb-floorquake", "spectral-implosion-explosion",
            "audio-fire", "beat-cubes", "spectrum-waves-3d", "spectral-particle-burst",
            "rgb-pulse-grid"
        ];
        string[] rejected =
        [
            "spectrum-tunnel", "fft-pillars", "spectrum-cube-walls", "bass-core", "frequency-orbit",
            "spectral-fountain", "frequency-vortex", "mirror-spectrum-cube", "beat-bloom",
            "tri-band-wave-space", "tri-band-rotating-space", "triple-orbit-shells",
            "reactive-helix", "audio-tornado"
        ];

        Assert.All(keepers, id => Assert.Contains(id, ids));
        Assert.All(rejected, id => Assert.DoesNotContain(id, ids));
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (var profile in DeviceProfiles.All)
        foreach (var visualizer in VisualizerRegistry.CreateAll())
            yield return [profile, visualizer];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void VisualizersRenderEveryPlannedSceneWithoutLeavingBounds(
        CubeDeviceProfile profile, IVisualizer visualizer)
    {
        var scene = new VoxelScene(profile.Width, profile.Depth, profile.Height);
        var audio = ActiveAudio(isBeat: true);

        visualizer.Render(scene, audio, new VisualizationFrame(TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(50), 20, profile.Capabilities));

        Assert.True(scene.CountLitVoxels() > 0, $"{visualizer.DisplayName} rendered a black scene.");
    }

    [Fact]
    public void EveryVisualizerProducesMonochromeColorsForMonochromeDevices()
    {
        foreach (var visualizer in VisualizerRegistry.CreateAll())
        {
            var scene = new VoxelScene(16, 16, 16);
            visualizer.Render(scene, ActiveAudio(isBeat: true), new VisualizationFrame(
                TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(50), 20,
                DeviceCapabilities.MonochromeIntensity));

            for (var x = 0; x < scene.Width; x++)
            for (var y = 0; y < scene.Depth; y++)
            for (var z = 0; z < scene.Height; z++)
            {
                var color = scene[x, y, z];
                Assert.True(color.R == color.G && color.G == color.B,
                    $"{visualizer.DisplayName} emitted RGB color on a monochrome profile.");
            }
        }
    }

    [Fact]
    public void StatefulVisualizersKeepFixedCapacityDuringLongRuns()
    {
        foreach (var visualizer in VisualizerRegistry.CreateAll())
        {
            if (visualizer is not IBoundedVisualizerState state) continue;
            var initialCapacity = state.StateCapacity;
            var scene = new VoxelScene(8, 8, 32);
            for (var frameNumber = 0; frameNumber < 300; frameNumber++)
            {
                visualizer.Render(scene, ActiveAudio(frameNumber % 17 == 0), new VisualizationFrame(
                    TimeSpan.FromMilliseconds(frameNumber * 50), TimeSpan.FromMilliseconds(50), frameNumber,
                    DeviceCapabilities.Rgb));
            }

            Assert.InRange(initialCapacity, 1, 1024);
            Assert.Equal(initialCapacity, state.StateCapacity);
        }
    }

    [Fact]
    public void EveryRegisteredVisualizerIsBlackAtZeroSensitivityAndResumesImmediately()
    {
        foreach (var visualizer in VisualizerRegistry.CreateAll())
        {
            var scene = new VoxelScene(8, 8, 8);
            var frame = new VisualizationFrame(TimeSpan.FromSeconds(1),
                TimeSpan.FromMilliseconds(50), 20, DeviceCapabilities.Rgb);

            visualizer.Render(scene, ActiveAudio(isBeat: true), frame);
            var staleActiveSnapshot = ActiveAudio(isBeat: true);
            VisualizerRenderer.Render(visualizer, scene, staleActiveSnapshot,
                frame with { FrameNumber = 21 }, sensitivityPercent: 0);
            Assert.Equal(0, scene.CountLitVoxels());

            VisualizerRenderer.Render(visualizer, scene, ActiveAudio(isBeat: true),
                frame with { FrameNumber = 22 }, sensitivityPercent: 50);
            Assert.True(scene.CountLitVoxels() > 0,
                $"{visualizer.DisplayName} did not resume after zero-sensitivity mute.");
        }
    }

    [Fact]
    public void FrequencyShockwavesUsesTheFullSensitivityRange()
    {
        var subtle = RenderShockwaves(.15);
        var useful = RenderShockwaves(.50);
        var aggressive = RenderShockwaves(1.0);
        var spectrumAtFifty = RenderMode("eight-band-spectrum", .50);
        var firestormAtFifty = RenderMode("frequency-firestorm", .50);

        Assert.InRange(subtle, 1, 80);
        Assert.True(useful > subtle, $"Expected 50% activity ({useful}) above 15% ({subtle}).");
        Assert.True(aggressive > useful, $"Expected 100% activity ({aggressive}) above 50% ({useful}).");
        Assert.True(useful < 400, $"50% response is already near cube saturation: {useful} lit voxels.");
        Assert.InRange(useful, Math.Min(spectrumAtFifty, firestormAtFifty) / 2,
            Math.Max(spectrumAtFifty, firestormAtFifty) * 2);
    }

    [Fact]
    public void BassShockwaveTraversesTheCubeWithinHalfASecondAndKeepsBoundedWaves()
    {
        var visualizer = VisualizerRegistry.CreateAll().Single(item => item.Id == "bass-shockwave");
        var scene = new VoxelScene(8, 8, 8);
        for (var frameNumber = 0; frameNumber < 8; frameNumber++)
        {
            var audio = ActiveAudio(isBeat: frameNumber is 0 or 3);
            visualizer.Render(scene, audio, new VisualizationFrame(
                TimeSpan.FromMilliseconds(frameNumber * 55), TimeSpan.FromMilliseconds(55),
                frameNumber, DeviceCapabilities.Rgb));
        }

        Assert.True(scene[0, 0, 0].IsLit, "Shockwave did not reach the outer cube within 440 ms.");
        var state = Assert.IsAssignableFrom<IBoundedVisualizerState>(visualizer);
        Assert.InRange(state.StateCapacity, 8, 16);
    }

    [Fact]
    public void SpectralPlasmaRespondsProgressivelyAcrossSensitivityRange()
    {
        string[] comparisonModes =
        [
            "eight-band-spectrum", "bass-mid-treble-space",
            "frequency-firestorm", "spectral-plasma"
        ];
        double[] sensitivityScales = [.10, .25, .50, .75, 1.0];
        var activity = comparisonModes.ToDictionary(
            id => id,
            id => sensitivityScales.Select(scale => RenderMode(id, scale)).ToArray());
        var plasma = activity["spectral-plasma"];

        Assert.InRange(plasma[0], 0, 32);
        Assert.InRange(plasma[1], 24, 240);
        Assert.InRange(plasma[2], 150, 430);
        Assert.True(plasma[2] > plasma[1], $"50% plasma activity did not exceed 25%: [{string.Join(", ", plasma)}]");
        Assert.True(plasma[3] > plasma[2], $"75% plasma activity did not exceed 50%: [{string.Join(", ", plasma)}]");
        Assert.True(plasma[4] > plasma[3], $"100% plasma activity did not exceed 75%: [{string.Join(", ", plasma)}]");
        Assert.True(plasma[1] < 384, $"Spectral Plasma is saturated at 25%: {plasma[1]} voxels.");
    }

    [Fact]
    public void NewActiveModesRespondAtNormalSensitivityWithoutRequiringBoostAboveHundredPercent()
    {
        string[] newModeIds =
        [
            "rgb-supernova", "beat-fireworks", "spectral-crossfire", "frequency-storm",
            "beat-comets", "rgb-floorquake", "spectral-implosion-explosion", "audio-fire",
            "beat-cubes", "spectrum-waves-3d", "spectral-particle-burst", "rgb-pulse-grid"
        ];

        foreach (var id in newModeIds)
        {
            var subdued = RenderPeakMode(id, .25);
            var normal = RenderPeakMode(id, .50);
            var aggressive = RenderPeakMode(id, 1.0);
            Assert.True(subdued > 0, $"{id} is black at 25% sensitivity.");
            Assert.True(normal > 0, $"{id} is black at 50% sensitivity.");
            Assert.True(aggressive > 0, $"{id} is black at 100% sensitivity.");
        }
    }

    private static int RenderShockwaves(double sensitivityScale)
        => RenderMode("frequency-shockwaves", sensitivityScale);

    private static int RenderPeakMode(string visualizerId, double sensitivityScale)
    {
        var visualizer = VisualizerRegistry.CreateAll().Single(item => item.Id == visualizerId);
        var scene = new VoxelScene(8, 8, 8);
        var bands = ActiveBands.Select(value => value * sensitivityScale).ToArray();
        var audio = new AudioAnalysisSnapshot(.6, .58, .62, bands, bands, bands,
            bands.Take(2).Max(), bands.Skip(2).Take(3).Average(), bands.Skip(5).Average(),
            true, .9, DateTimeOffset.UtcNow)
        {
            SensitivityPercent = (int)Math.Round(sensitivityScale * 100)
        };
        var peak = 0;
        for (var frameNumber = 0; frameNumber < 20; frameNumber++)
        {
            VisualizerRenderer.Render(visualizer, scene, audio,
                new VisualizationFrame(TimeSpan.FromMilliseconds(frameNumber * 50),
                    TimeSpan.FromMilliseconds(50), frameNumber, DeviceCapabilities.Rgb));
            peak = Math.Max(peak, scene.CountLitVoxels());
        }
        return peak;
    }

    private static int RenderMode(string visualizerId, double sensitivityScale)
    {
        var visualizer = VisualizerRegistry.CreateAll().Single(item => item.Id == visualizerId);
        var scene = new VoxelScene(8, 8, 8);
        var bands = ActiveBands.Select(value => value * sensitivityScale).ToArray();
        var audio = new AudioAnalysisSnapshot(.6, .58, .62, bands, bands, bands,
            bands.Take(2).Max(), bands.Skip(2).Take(3).Average(), bands.Skip(5).Average(),
            true, .9, DateTimeOffset.UtcNow)
        {
            SensitivityPercent = (int)Math.Round(sensitivityScale * 100)
        };
        for (var frameNumber = 0; frameNumber < 20; frameNumber++)
            VisualizerRenderer.Render(visualizer, scene, audio,
                new VisualizationFrame(TimeSpan.FromMilliseconds(frameNumber * 50),
                    TimeSpan.FromMilliseconds(50), frameNumber, DeviceCapabilities.Rgb));
        return scene.CountLitVoxels();
    }

    private static AudioAnalysisSnapshot ActiveAudio(bool isBeat) => new(
        .6, .58, .62, ActiveBands, ActiveBands, ActiveBands,
        .79, .52, .27, isBeat, isBeat ? .9 : .25, DateTimeOffset.UtcNow);
}
