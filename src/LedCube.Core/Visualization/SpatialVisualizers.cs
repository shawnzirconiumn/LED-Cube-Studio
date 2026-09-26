using LedCube.Core.Audio;
using LedCube.Core.Scene;

namespace LedCube.Core.Visualization;

internal sealed class TriBandRotatingSpaceVisualizer() : VisualizerBase(
    "tri-band-rotating-space", "Tri-Band Rotating Space", "Bass, mids, and treble orbit on three perpendicular planes.",
    VisualizerCategory.Spatial, new VisualizerResponse(1.2, .9, .01))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        for (var group = 0; group < 3; group++)
        {
            var level = group == 0 ? VisualizerMath.Average(bands, 0, 1) :
                group == 1 ? VisualizerMath.Average(bands, 2, 4) : VisualizerMath.Average(bands, 5, 7);
            if (level <= .02) continue;
            VisualizerGeometry.DrawOrbit(scene, group, .2 + level * .75,
                frame.Elapsed.TotalSeconds * (group % 2 == 0 ? 1 : -1) + group,
                Math.Max(16, Math.Max(scene.Width, Math.Max(scene.Depth, scene.Height)) * 4),
                VisualizerMath.Color(group * 3, frame.Capabilities, level));
        }
    }
}

internal sealed class TriBandSplitCubeVisualizer() : VisualizerBase(
    "tri-band-split-cube", "Tri-Band Split Cube", "Bass, midrange, and treble occupy animated thirds of the cube.",
    VisualizerCategory.Spatial, new VisualizerResponse(.85, 1.1, .03))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        for (var x = 0; x < scene.Width; x++)
        {
            var group = Math.Min(2, x * 3 / scene.Width);
            var level = group == 0 ? VisualizerMath.Average(bands, 0, 1) :
                group == 1 ? VisualizerMath.Average(bands, 2, 4) : VisualizerMath.Average(bands, 5, 7);
            var height = VisualizerMath.Height(level, scene.Height);
            var phase = frame.Elapsed.TotalSeconds * (group + 1);
            for (var y = 0; y < scene.Depth; y++)
            {
                var wave = (int)Math.Round(Math.Sin(VisualizerMath.Unit(y, scene.Depth) * Math.Tau + phase) * level * 2);
                for (var z = 0; z < height; z++)
                {
                    var shiftedZ = Math.Clamp(z + wave, 0, scene.Height - 1);
                    scene[x, y, shiftedZ] = VisualizerMath.Color(group * 3, frame.Capabilities, level);
                }
            }
        }
    }
}

internal sealed class BassCompressionVisualizer() : VisualizerBase(
    "bass-compression", "Bass Compression", "Bass squeezes a central shell while mids and highs break around it.",
    VisualizerCategory.Spatial, new VisualizerResponse(.9, 1.1, .03))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        var bass = VisualizerMath.Average(bands, 0, 1);
        var mids = VisualizerMath.Average(bands, 2, 4);
        var highs = VisualizerMath.Average(bands, 5, 7);
        if (bass > .02)
        {
            var wobble = .06 * Math.Sin(frame.Elapsed.TotalSeconds * 5);
            VisualizerGeometry.DrawBoxShell(scene, Math.Clamp(1 - bass * .72 + wobble, .12, 1),
                VisualizerMath.Color(0, frame.Capabilities, bass));
        }
        var innerPoints = (int)Math.Ceiling(mids * 24);
        for (var i = 0; i < innerPoints; i++)
        {
            var seed = frame.FrameNumber * 31 + i * 71L;
            scene.SetNormalized(.25 + VisualizerMath.Noise(seed) * .5,
                .25 + VisualizerMath.Noise(seed + 1) * .5,
                .25 + VisualizerMath.Noise(seed + 2) * .5,
                VisualizerMath.Color(3, frame.Capabilities, mids));
        }
        var outerPoints = (int)Math.Ceiling(highs * 18);
        for (var i = 0; i < outerPoints; i++)
        {
            var seed = frame.FrameNumber * 43 + i * 97L;
            var face = i % 6;
            var a = VisualizerMath.Noise(seed); var b = VisualizerMath.Noise(seed + 1);
            var x = face == 0 ? 0 : face == 1 ? 1 : a;
            var y = face == 2 ? 0 : face == 3 ? 1 : b;
            var z = face == 4 ? 0 : face == 5 ? 1 : VisualizerMath.Noise(seed + 2);
            scene.SetNormalized(x, y, z, VisualizerMath.Color(6, frame.Capabilities, highs));
        }
    }
}

internal sealed class BassFloorquakeVisualizer() : VisualizerBase(
    "bass-floorquake", "Bass Floorquake", "Bass ripples deform the floor while higher bands throw debris upward.",
    VisualizerCategory.Spatial, new VisualizerResponse(1.15, .9, .01))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        var bass = VisualizerMath.Average(bands, 0, 1);
        var mids = VisualizerMath.Average(bands, 2, 4);
        var highs = VisualizerMath.Average(bands, 5, 7);
        for (var x = 0; x < scene.Width; x++)
        for (var y = 0; y < scene.Depth; y++)
        {
            var dx = VisualizerMath.Centered(x, scene.Width);
            var dy = VisualizerMath.Centered(y, scene.Depth);
            var distance = Math.Sqrt(dx * dx + dy * dy);
            var wave = (.5 + .5 * Math.Sin(distance * 12 - frame.Elapsed.TotalSeconds * 8)) * bass;
            var z = VisualizerMath.Coord(Math.Min(.7, wave * .5), scene.Height);
            scene[x, y, z] = VisualizerMath.Color(distance < .5 ? 0 : 2, frame.Capabilities, bass);
            if (mids > .15 && ((x + y + frame.FrameNumber) & 3) == 0)
                scene[x, y, Math.Min(scene.Height - 1, z + VisualizerMath.Height(mids * .3, scene.Height))] =
                    VisualizerMath.Color(4, frame.Capabilities, mids);
        }
        for (var i = 0; i < (int)Math.Ceiling(highs * 16); i++)
        {
            var seed = frame.FrameNumber * 19 + i * 59L;
            scene.SetNormalized(VisualizerMath.Noise(seed), VisualizerMath.Noise(seed + 1),
                .3 + VisualizerMath.Noise(seed + 2) * .7,
                VisualizerMath.Color(7, frame.Capabilities, highs));
        }
    }
}

internal sealed class TripleOrbitShellsVisualizer() : VisualizerBase(
    "triple-orbit-shells", "Triple Orbit Shells", "Three frequency shells orbit across the cube's principal axes.",
    VisualizerCategory.Spatial, new VisualizerResponse(1.25, .9, .01))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        var points = Math.Max(20, Math.Max(scene.Width, Math.Max(scene.Depth, scene.Height)) * 5);
        for (var axis = 0; axis < 3; axis++)
        {
            var level = axis == 0 ? VisualizerMath.Average(bands, 0, 1) :
                axis == 1 ? VisualizerMath.Average(bands, 2, 4) : VisualizerMath.Average(bands, 5, 7);
            if (level <= .02) continue;
            var phase = frame.Elapsed.TotalSeconds * (axis + 1) * (axis == 1 ? -.7 : .7);
            VisualizerGeometry.DrawOrbit(scene, axis, .25 + level * .72, phase, points,
                VisualizerMath.Color(axis * 3, frame.Capabilities, level));
            VisualizerGeometry.DrawOrbit(scene, axis, .12 + level * .5, -phase * .7, points / 2,
                VisualizerMath.Color(axis * 3 + 1, frame.Capabilities, level));
        }
    }
}

internal sealed class ReactiveHelixVisualizer() : VisualizerBase(
    "reactive-helix", "Reactive Helix", "A double helix flexes with bass and gains treble rungs.",
    VisualizerCategory.Spatial, new VisualizerResponse(1.2, .9, .01))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        var bass = VisualizerMath.Average(bands, 0, 1);
        var mids = VisualizerMath.Average(bands, 2, 4);
        var highs = VisualizerMath.Average(bands, 5, 7);
        var steps = Math.Max(scene.Height * 3, 24);
        for (var i = 0; i < steps; i++)
        {
            var z = i / (double)(steps - 1);
            var angle = z * Math.Tau * (1.5 + mids) + frame.Elapsed.TotalSeconds * 2;
            var radius = .12 + bass * .3;
            var x1 = .5 + Math.Cos(angle) * radius;
            var y1 = .5 + Math.Sin(angle) * radius;
            var x2 = .5 - Math.Cos(angle) * radius;
            var y2 = .5 - Math.Sin(angle) * radius;
            scene.SetNormalized(x1, y1, z, VisualizerMath.Color(2, frame.Capabilities, Math.Max(bass, mids)));
            scene.SetNormalized(x2, y2, z, VisualizerMath.Color(6, frame.Capabilities, Math.Max(bass, mids)));
            if (highs > .1 && i % Math.Max(2, steps / Math.Max(2, scene.Height)) == 0)
                VisualizerGeometry.DrawLine(scene, x1, y1, z, x2, y2, z,
                    VisualizerMath.Color(7, frame.Capabilities, highs));
        }
    }
}

internal sealed class AudioTornadoVisualizer() : VisualizerBase(
    "audio-tornado", "Audio Tornado", "Spectrum strands spiral upward in a tapered audio vortex.",
    VisualizerCategory.Spatial, new VisualizerResponse(1.2, .9, .01))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        var steps = Math.Max(scene.Height * 4, 32);
        for (var band = 0; band < 8; band++)
        {
            if (bands[band] <= .02) continue;
            for (var i = 0; i < steps; i++)
            {
                var z = i / (double)(steps - 1);
                var radius = (.08 + z * .38) * (.35 + bands[band] * .65);
                var angle = z * Math.Tau * 2.5 + frame.Elapsed.TotalSeconds * 2.2 + band * Math.Tau / 8;
                scene.SetNormalized(.5 + Math.Cos(angle) * radius, .5 + Math.Sin(angle) * radius, z,
                    VisualizerMath.Color(band, frame.Capabilities, bands[band]));
            }
        }
    }
}
