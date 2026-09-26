using LedCube.Core.Scene;
using LedCube.Core.Devices;

namespace LedCube.Core.Visualization;

internal static class VisualizerPalette
{
    private static readonly VoxelColor[] Colors =
    [
        VoxelColor.Red,
        VoxelColor.FromRgb(255, 128, 0),
        VoxelColor.Yellow,
        VoxelColor.Green,
        VoxelColor.Cyan,
        VoxelColor.Blue,
        VoxelColor.FromRgb(128, 0, 255),
        VoxelColor.Magenta
    ];

    public static VoxelColor At(double normalized) =>
        Colors[Math.Clamp((int)Math.Round(Math.Clamp(normalized, 0, 1) * (Colors.Length - 1)), 0, Colors.Length - 1)];

    public static VoxelColor ForBand(int band) => Colors[Math.Clamp(band, 0, Colors.Length - 1)];

    public static VoxelColor Adapt(VoxelColor color, DeviceCapabilities capabilities) =>
        capabilities == DeviceCapabilities.Rgb ? color : VoxelColor.Monochrome(color.Intensity);
}
