using LedCube.Core.Scene;

namespace LedCube.App.ViewModels;

public sealed record StreamModeOption(string Name, VoxelColor? StaticColor)
{
    public bool UsesAudio => StaticColor is null;

    public override string ToString() => Name;
}
