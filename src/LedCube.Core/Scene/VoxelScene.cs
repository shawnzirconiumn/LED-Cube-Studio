namespace LedCube.Core.Scene;

/// <summary>A logical three-dimensional volume. No wire-format assumptions live here.</summary>
public sealed class VoxelScene
{
    private readonly VoxelColor[] _voxels;

    public VoxelScene(int width, int depth, int height)
    {
        if (width <= 0 || depth <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "All scene dimensions must be positive.");

        Width = width;
        Depth = depth;
        Height = height;
        _voxels = new VoxelColor[checked(width * depth * height)];
    }

    public int Width { get; }
    public int Depth { get; }
    public int Height { get; }

    public VoxelColor this[int x, int y, int z]
    {
        get => _voxels[GetIndex(x, y, z)];
        set => _voxels[GetIndex(x, y, z)] = value;
    }

    public void Clear(VoxelColor color = default) => Array.Fill(_voxels, color);

    public void SetNormalized(double x, double y, double z, VoxelColor color)
    {
        this[ScaleCoordinate(x, Width), ScaleCoordinate(y, Depth), ScaleCoordinate(z, Height)] = color;
    }

    public bool TrySet(int x, int y, int z, VoxelColor color)
    {
        if ((uint)x >= Width || (uint)y >= Depth || (uint)z >= Height)
            return false;
        _voxels[x + Width * (y + Depth * z)] = color;
        return true;
    }

    public int CountLitVoxels() => _voxels.Count(voxel => voxel.IsLit);

    public VoxelSceneStatistics GetStatistics()
    {
        var litVoxelCount = 0;
        byte maxIntensity = 0;
        byte maxRed = 0;
        byte maxGreen = 0;
        byte maxBlue = 0;
        foreach (var voxel in _voxels)
        {
            if (voxel.IsLit) litVoxelCount++;
            maxIntensity = Math.Max(maxIntensity, voxel.Intensity);
            maxRed = Math.Max(maxRed, voxel.R);
            maxGreen = Math.Max(maxGreen, voxel.G);
            maxBlue = Math.Max(maxBlue, voxel.B);
        }
        return new VoxelSceneStatistics(litVoxelCount, maxIntensity, maxRed, maxGreen, maxBlue);
    }

    public static int ScaleCoordinate(double normalized, int size) =>
        Math.Clamp((int)Math.Round(Math.Clamp(normalized, 0, 1) * (size - 1)), 0, size - 1);

    private int GetIndex(int x, int y, int z)
    {
        if ((uint)x >= Width || (uint)y >= Depth || (uint)z >= Height)
            throw new ArgumentOutOfRangeException($"({x}, {y}, {z}) is outside {Width}x{Depth}x{Height}.");
        return x + Width * (y + Depth * z);
    }
}

public readonly record struct VoxelSceneStatistics(
    int LitVoxelCount,
    byte MaxIntensity,
    byte MaxRed,
    byte MaxGreen,
    byte MaxBlue);
