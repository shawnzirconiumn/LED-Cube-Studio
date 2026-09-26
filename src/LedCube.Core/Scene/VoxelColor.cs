namespace LedCube.Core.Scene;

/// <summary>A device-independent RGB voxel with an explicit monochrome intensity.</summary>
public readonly record struct VoxelColor(byte R, byte G, byte B, byte Intensity)
{
    public static readonly VoxelColor Black = new(0, 0, 0, 0);
    public static readonly VoxelColor White = FromRgb(255, 255, 255);
    public static readonly VoxelColor Red = FromRgb(255, 0, 0);
    public static readonly VoxelColor Green = FromRgb(0, 255, 0);
    public static readonly VoxelColor Blue = FromRgb(0, 0, 255);
    public static readonly VoxelColor Cyan = FromRgb(0, 255, 255);
    public static readonly VoxelColor Magenta = FromRgb(255, 0, 255);
    public static readonly VoxelColor Yellow = FromRgb(255, 255, 0);

    public bool IsLit => Intensity > 0 || R > 0 || G > 0 || B > 0;

    public static VoxelColor FromRgb(byte red, byte green, byte blue)
    {
        var intensity = (byte)Math.Clamp(
            (int)Math.Round(red * 0.2126 + green * 0.7152 + blue * 0.0722), 0, 255);
        return new VoxelColor(red, green, blue, intensity);
    }

    public static VoxelColor Monochrome(byte intensity) =>
        new(intensity, intensity, intensity, intensity);
}
