using LedCube.Core.Audio;
using LedCube.Core.Scene;

namespace LedCube.Core.Visualization;

/// <summary>Applies output invariants shared by every registered FFT visualizer.</summary>
public static class VisualizerRenderer
{
    public static void Render(
        IVisualizer visualizer,
        VoxelScene scene,
        AudioAnalysisSnapshot audio,
        VisualizationFrame frame,
        int? sensitivityPercent = null)
    {
        ArgumentNullException.ThrowIfNull(visualizer);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(audio);

        // Render first so bounded particle/history state can continue advancing.
        // Sensitivity zero is an output mute and does not reset shared analysis state.
        visualizer.Render(scene, audio, frame);
        if ((sensitivityPercent ?? audio.SensitivityPercent) <= 0)
            scene.Clear();
    }
}
