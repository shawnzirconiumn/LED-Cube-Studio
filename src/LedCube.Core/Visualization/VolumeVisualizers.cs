using LedCube.Core.Audio;
using LedCube.Core.Scene;

namespace LedCube.Core.Visualization;

internal sealed class SpectralGalaxyVisualizer() : VisualizerBase(
    "spectral-galaxy", "Spectral Galaxy", "Spectrum-colored spiral arms rotate around a bass-bright core.",
    VisualizerCategory.Volume, new VisualizerResponse(1.2, .9, .01))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        var bass = VisualizerMath.Average(bands, 0, 1);
        var points = Math.Max(48, Math.Max(scene.Width, scene.Depth) * 10);
        for (var arm = 0; arm < 3; arm++)
        for (var point = 0; point < points; point++)
        {
            var t = point / (double)(points - 1);
            var band = Math.Min(7, (int)(t * 8));
            if (bands[band] <= .02) continue;
            var angle = arm * Math.Tau / 3 + t * Math.Tau * 2.2 + frame.Elapsed.TotalSeconds * .45;
            var radius = t * .47 * (.45 + bands[band] * .55);
            var z = .5 + Math.Sin(angle * 1.5) * (.04 + bands[band] * .12);
            scene.SetNormalized(.5 + Math.Cos(angle) * radius, .5 + Math.Sin(angle) * radius, z,
                VisualizerMath.Color(band, frame.Capabilities, bands[band]));
        }
        if (bass > .02) VisualizerGeometry.DrawBoxShell(scene, .05 + bass * .16,
            VisualizerMath.Color(0, frame.Capabilities, bass));
    }
}

internal sealed class AudioNebulaVisualizer() : VisualizerBase(
    "audio-nebula", "Audio Nebula", "A drifting, cloudy spectrum volume with changing color regions.",
    VisualizerCategory.Volume, new VisualizerResponse(1.4, .85, .01))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        var time = frame.Elapsed.TotalSeconds;
        for (var x = 0; x < scene.Width; x++)
        for (var y = 0; y < scene.Depth; y++)
        for (var z = 0; z < scene.Height; z++)
        {
            var nx = VisualizerMath.Centered(x, scene.Width);
            var ny = VisualizerMath.Centered(y, scene.Depth);
            var nz = VisualizerMath.Centered(z, scene.Height);
            var band = Math.Clamp((int)((VisualizerMath.Unit(z, scene.Height) * .55 +
                VisualizerMath.Unit(x, scene.Width) * .45) * 8), 0, 7);
            var cloud = .5 + .25 * Math.Sin(nx * 5 + time) + .2 * Math.Sin(ny * 7 - time * .7) +
                        .15 * Math.Cos(nz * 6 + time * .45);
            var falloff = Math.Max(0, 1 - (nx * nx + ny * ny + nz * nz) * .35);
            if (cloud * falloff * bands[band] > .18)
                scene[x, y, z] = VisualizerMath.Color(band, frame.Capabilities, bands[band]);
        }
    }
}

internal sealed class ReactivePlasmaVolumeVisualizer() : VisualizerBase(
    "reactive-plasma-volume", "Reactive Plasma Volume", "Three-dimensional waves bend independently to bass, mids, and treble.",
    VisualizerCategory.Volume, new VisualizerResponse(1.25, .9, .02))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        var bass = VisualizerMath.Average(bands, 0, 1);
        var mids = VisualizerMath.Average(bands, 2, 4);
        var highs = VisualizerMath.Average(bands, 5, 7);
        var time = frame.Elapsed.TotalSeconds;
        for (var x = 0; x < scene.Width; x++)
        for (var y = 0; y < scene.Depth; y++)
        for (var z = 0; z < scene.Height; z++)
        {
            var nx = VisualizerMath.Unit(x, scene.Width) * Math.Tau;
            var ny = VisualizerMath.Unit(y, scene.Depth) * Math.Tau;
            var nz = VisualizerMath.Unit(z, scene.Height) * Math.Tau;
            var field = Math.Sin(nx * (1 + bass) + time * 2) * bass +
                        Math.Sin(ny * (1 + mids * 2) - time * 1.4) * mids +
                        Math.Cos(nz * (1 + highs * 3) + time * 1.8) * highs;
            if (Math.Abs(field) < .42) continue;
            var band = field > .65 ? 7 : field > 0 ? 4 : field < -.65 ? 0 : 2;
            scene[x, y, z] = VisualizerMath.Color(band, frame.Capabilities, Math.Min(1, Math.Abs(field)));
        }
    }
}
