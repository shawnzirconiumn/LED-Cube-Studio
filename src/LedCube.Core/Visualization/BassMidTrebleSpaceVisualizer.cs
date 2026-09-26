using LedCube.Core.Audio;
using LedCube.Core.Scene;

namespace LedCube.Core.Visualization;

public sealed class BassMidTrebleSpaceVisualizer : IVisualizer
{
    public string Id => "bass-mid-treble-space";
    public string DisplayName => "Bass/Mid/Treble Space";
    public string Description => "Bass shells, midrange rings, and treble sparks in one spatial scene.";
    public VisualizerCategory Category => VisualizerCategory.Spatial;
    public Devices.DeviceCapabilities? RequiredCapability => null;

    public void Render(VoxelScene scene, AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        scene.Clear();
        DrawBassShell(scene, audio.Bass, frame.Capabilities);

        for (var band = 2; band <= 4; band++)
        {
            var level = audio.Spectrum.Count > band ? audio.Spectrum[band] : 0;
            if (level <= 0.02) continue;
            var radius = (band - 1) / 4.0;
            var z = VoxelScene.ScaleCoordinate(level, scene.Height);
            DrawSquareRing(scene, radius, z,
                VisualizerPalette.Adapt(VisualizerPalette.ForBand(band), frame.Capabilities));
        }

        for (var band = 5; band < 8; band++)
        {
            var level = audio.Spectrum.Count > band ? audio.Spectrum[band] : 0;
            var points = level <= 0.02 ? 0 : Math.Max(1,
                (int)Math.Ceiling(level * Math.Max(scene.Width, Math.Max(scene.Depth, scene.Height))));
            for (var point = 0; point < points; point++)
            {
                var seed = unchecked(frame.FrameNumber * 17 + band * 31L + point * 13L);
                var x = PositiveModulo(seed, scene.Width);
                var y = PositiveModulo(seed / 3 + band * 2L, scene.Depth);
                var z = PositiveModulo(seed / 5 + point * 3L, scene.Height);
                scene[x, y, z] = VisualizerPalette.Adapt(VisualizerPalette.ForBand(band), frame.Capabilities);
            }
        }
    }

    private static void DrawBassShell(VoxelScene scene, double bass, Devices.DeviceCapabilities capabilities)
    {
        if (bass <= 0.02) return;
        var radius = Math.Clamp(bass, 1.0 / Math.Max(2, Math.Min(scene.Width, scene.Depth)), 1);
        var halfX = Math.Max(0, (int)Math.Ceiling(radius * (scene.Width - 1) / 2));
        var halfY = Math.Max(0, (int)Math.Ceiling(radius * (scene.Depth - 1) / 2));
        var halfZ = Math.Max(0, (int)Math.Ceiling(radius * (scene.Height - 1) / 2));
        var minX = Math.Max(0, (scene.Width - 1) / 2 - halfX);
        var maxX = Math.Min(scene.Width - 1, scene.Width / 2 + halfX);
        var minY = Math.Max(0, (scene.Depth - 1) / 2 - halfY);
        var maxY = Math.Min(scene.Depth - 1, scene.Depth / 2 + halfY);
        var minZ = Math.Max(0, (scene.Height - 1) / 2 - halfZ);
        var maxZ = Math.Min(scene.Height - 1, scene.Height / 2 + halfZ);

        for (var x = minX; x <= maxX; x++)
        for (var y = minY; y <= maxY; y++)
        for (var z = minZ; z <= maxZ; z++)
            if (x == minX || x == maxX || y == minY || y == maxY || z == minZ || z == maxZ)
                scene[x, y, z] = VisualizerPalette.Adapt(VisualizerPalette.ForBand(0), capabilities);
    }

    private static void DrawSquareRing(VoxelScene scene, double normalizedRadius, int z, VoxelColor color)
    {
        var halfX = Math.Max(1, (int)Math.Round(normalizedRadius * (scene.Width - 1) / 2));
        var halfY = Math.Max(1, (int)Math.Round(normalizedRadius * (scene.Depth - 1) / 2));
        var minX = Math.Max(0, (scene.Width - 1) / 2 - halfX);
        var maxX = Math.Min(scene.Width - 1, scene.Width / 2 + halfX);
        var minY = Math.Max(0, (scene.Depth - 1) / 2 - halfY);
        var maxY = Math.Min(scene.Depth - 1, scene.Depth / 2 + halfY);
        for (var x = minX; x <= maxX; x++)
        {
            scene[x, minY, z] = color;
            scene[x, maxY, z] = color;
        }
        for (var y = minY + 1; y < maxY; y++)
        {
            scene[minX, y, z] = color;
            scene[maxX, y, z] = color;
        }
    }

    private static int PositiveModulo(long value, int modulus) => (int)((value % modulus + modulus) % modulus);
}
