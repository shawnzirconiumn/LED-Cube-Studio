namespace LedCube.Core.Visualization;

/// <summary>Creates the curated visualizer catalog in selector display order.</summary>
public static class VisualizerRegistry
{
    public static IReadOnlyList<IVisualizer> CreateAll() =>
    [
        new EightBandSpectrumVisualizer(),
        new SpectrumWaterfallVisualizer(),
        new EqCityVisualizer(),

        new BassMidTrebleSpaceVisualizer(),
        new TriBandSplitCubeVisualizer(),
        new BassCompressionVisualizer(),
        new BassFloorquakeVisualizer(),
        new SpectralCrossfireVisualizer(),
        new SpectrumWaves3DVisualizer(),
        new RgbPulseGridVisualizer(),

        new BeatPulseDetailVisualizer(),
        new BassShockwaveVisualizer(),
        new BeatExplosionVisualizer(),
        new SpectralSupernovaVisualizer(),
        new FrequencyShockwavesVisualizer(),
        new SpectralLightningStormVisualizer(),
        new RgbSupernovaVisualizer(),
        new RgbFloorquakeVisualizer(),
        new SpectralImplosionExplosionVisualizer(),
        new BeatCubesVisualizer(),

        new SpectralRainVisualizer(),
        new FrequencyFirestormVisualizer(),
        new BeatFireworksVisualizer(),
        new FrequencyStormVisualizer(),
        new BeatCometsVisualizer(),
        new AudioFireVisualizer(),
        new SpectralParticleBurstVisualizer(),

        new SpectralPlasmaVisualizer(),
        new SpectralGalaxyVisualizer(),
        new AudioNebulaVisualizer(),
        new ReactivePlasmaVolumeVisualizer()
    ];
}
