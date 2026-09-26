using LedCube.Core.Audio;
using LedCube.Core.Scene;

namespace LedCube.Core.Visualization;

public sealed class EightBandSpectrumVisualizer : IVisualizer
{
    public string Id => "eight-band-spectrum";
    public string DisplayName => "8-Band Spectrum";
    public string Description => "Eight frequency bands drawn as full-depth level bars.";
    public VisualizerCategory Category => VisualizerCategory.Spectrum;
    public Devices.DeviceCapabilities? RequiredCapability => null;

    public void Render(VoxelScene scene, AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        scene.Clear();
        for (var x = 0; x < scene.Width; x++)
        {
            var band = Math.Min(7, x * 8 / scene.Width);
            var level = audio.Spectrum.Count > band ? audio.Spectrum[band] : 0;
            var height = level <= 0.02 ? 0 : Math.Clamp((int)Math.Ceiling(level * scene.Height), 1, scene.Height);
            for (var z = 0; z < height; z++)
            {
                var color = VisualizerPalette.Adapt(
                    VisualizerPalette.At(z / (double)Math.Max(1, scene.Height - 1)), frame.Capabilities);
                for (var y = 0; y < scene.Depth; y++)
                    scene[x, y, z] = color;
            }
        }
    }
}
