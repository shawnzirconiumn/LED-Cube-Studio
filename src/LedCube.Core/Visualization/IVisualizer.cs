using LedCube.Core.Audio;
using LedCube.Core.Devices;
using LedCube.Core.Scene;

namespace LedCube.Core.Visualization;

public readonly record struct VisualizationFrame(
    TimeSpan Elapsed,
    TimeSpan Delta,
    long FrameNumber,
    DeviceCapabilities Capabilities);

public interface IVisualizer
{
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }
    VisualizerCategory Category { get; }
    DeviceCapabilities? RequiredCapability { get; }
    void Render(VoxelScene scene, AudioAnalysisSnapshot audio, VisualizationFrame frame);
}

public enum VisualizerCategory
{
    Spectrum,
    Spatial,
    Beat,
    Particles,
    Volume
}

/// <summary>Exposes a fixed upper bound for visualizers that retain animation state.</summary>
public interface IBoundedVisualizerState
{
    int StateCapacity { get; }
}
