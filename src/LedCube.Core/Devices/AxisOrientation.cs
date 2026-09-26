namespace LedCube.Core.Devices;

public enum LogicalAxis { X, Y, Z }

/// <summary>Maps logical scene coordinates onto the device's physical axes.</summary>
public sealed record AxisOrientation(
    LogicalAxis PhysicalXFrom,
    LogicalAxis PhysicalYFrom,
    LogicalAxis PhysicalZFrom,
    bool ReverseX = false,
    bool ReverseY = false,
    bool ReverseZ = false)
{
    public static AxisOrientation Identity { get; } = new(LogicalAxis.X, LogicalAxis.Y, LogicalAxis.Z);

    public (int X, int Y, int Z) Map(int x, int y, int z, int width, int depth, int height)
    {
        var values = new[] { x, y, z };
        var sizes = new[] { width, depth, height };
        var px = values[(int)PhysicalXFrom];
        var py = values[(int)PhysicalYFrom];
        var pz = values[(int)PhysicalZFrom];
        if (ReverseX) px = sizes[(int)PhysicalXFrom] - 1 - px;
        if (ReverseY) py = sizes[(int)PhysicalYFrom] - 1 - py;
        if (ReverseZ) pz = sizes[(int)PhysicalZFrom] - 1 - pz;
        return (px, py, pz);
    }
}
