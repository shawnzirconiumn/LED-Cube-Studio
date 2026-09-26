using LedCube.Core.Scene;

namespace LedCube.Tests;

public sealed class VoxelSceneTests
{
    [Theory]
    [InlineData(8, 8, 8)]
    [InlineData(8, 8, 32)]
    [InlineData(16, 16, 16)]
    public void SceneUsesAllConfiguredDimensions(int width, int depth, int height)
    {
        var scene = new VoxelScene(width, depth, height);
        scene[width - 1, depth - 1, height - 1] = VoxelColor.White;
        Assert.Equal(VoxelColor.White, scene[width - 1, depth - 1, height - 1]);
        Assert.Equal(1, scene.CountLitVoxels());
    }

    [Fact]
    public void NormalizedCoordinatesReachBothExtremes()
    {
        var scene = new VoxelScene(8, 8, 32);
        scene.SetNormalized(0, 0, 0, VoxelColor.Red);
        scene.SetNormalized(1, 1, 1, VoxelColor.Blue);
        Assert.Equal(VoxelColor.Red, scene[0, 0, 0]);
        Assert.Equal(VoxelColor.Blue, scene[7, 7, 31]);
    }

    [Fact]
    public void StatisticsExposeLitCountAndMaximumColorComponents()
    {
        var scene = new VoxelScene(2, 2, 2);
        scene[0, 0, 0] = VoxelColor.FromRgb(20, 100, 40);
        scene[1, 1, 1] = VoxelColor.FromRgb(200, 30, 250);

        var statistics = scene.GetStatistics();

        Assert.Equal(2, statistics.LitVoxelCount);
        Assert.Equal(200, statistics.MaxRed);
        Assert.Equal(100, statistics.MaxGreen);
        Assert.Equal(250, statistics.MaxBlue);
        Assert.Equal(82, statistics.MaxIntensity);
    }
}
