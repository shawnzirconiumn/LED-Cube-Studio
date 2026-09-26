using LedCube.Core.Audio;
using LedCube.Core.Scene;

namespace LedCube.Core.Visualization;

internal sealed class SpectrumWaterfallVisualizer() : HistoryVisualizerBase(
    "spectrum-waterfall", "Spectrum Waterfall", "A scrolling history of all eight spectrum bands.",
    VisualizerCategory.Spectrum, new VisualizerResponse())
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        PushHistory(bands);
        for (var y = 0; y < scene.Depth; y++)
        {
            var age = Math.Min(HistoryLength - 1, y * HistoryLength / scene.Depth);
            for (var x = 0; x < scene.Width; x++)
            {
                var band = Math.Min(7, x * 8 / scene.Width);
                var height = VisualizerMath.Height(History[band, age], scene.Height);
                var color = VisualizerMath.Color(band, frame.Capabilities, History[band, age]);
                for (var z = 0; z < height; z++) scene[x, y, z] = color;
            }
        }
    }
}

internal sealed class EqCityVisualizer() : VisualizerBase(
    "eq-city", "EQ City", "Spectrum-driven towers arranged around a rotating city block.",
    VisualizerCategory.Spectrum, new VisualizerResponse(.85, 1.1, .01))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        var phase = frame.Elapsed.TotalSeconds * .35;
        for (var band = 0; band < 8; band++)
        {
            var angle = phase + band * Math.Tau / 8;
            var x = VisualizerMath.Coord(.5 + Math.Cos(angle) * .42, scene.Width);
            var y = VisualizerMath.Coord(.5 + Math.Sin(angle) * .42, scene.Depth);
            var innerX = VisualizerMath.Coord(.5 + Math.Cos(angle) * .22, scene.Width);
            var innerY = VisualizerMath.Coord(.5 + Math.Sin(angle) * .22, scene.Depth);
            var height = VisualizerMath.Height(bands[band], scene.Height, .01);
            var color = VisualizerMath.Color(band, frame.Capabilities, bands[band]);
            for (var z = 0; z < height; z++)
            {
                scene[x, y, z] = color;
                scene[innerX, innerY, z] = color;
            }
        }
    }
}

internal sealed class BeatPulseDetailVisualizer() : BeatVisualizerBase(
    "beat-pulse-detail", "Beat Pulse + Detail", "Expanding bass pulses with mid lines and treble detail.",
    VisualizerCategory.Beat, new VisualizerResponse(.9, 1.1, .01))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        UpdateBeat(audio, frame, .4);
        var bass = VisualizerMath.Average(bands, 0, 1);
        var radius = Math.Clamp(Math.Max(bass * .75, PulseAge < .8 ? PulseAge / .8 : 0), .08, 1);
        if (bass > .02 || PulseAge < .8)
            VisualizerGeometry.DrawBoxShell(scene, radius,
                VisualizerMath.Color(0, frame.Capabilities, Math.Max(bass, 1 - PulseAge)));

        for (var band = 2; band <= 4; band++)
        {
            var x = VisualizerMath.Coord((band - 1) / 4d, scene.Width);
            var mirror = scene.Width - 1 - x;
            var height = VisualizerMath.Height(bands[band], scene.Height);
            for (var z = 0; z < height; z++)
            {
                scene[x, scene.Depth / 2, z] = VisualizerMath.Color(band, frame.Capabilities, bands[band]);
                scene[mirror, scene.Depth / 2, z] = VisualizerMath.Color(band, frame.Capabilities, bands[band]);
            }
        }

        var detail = VisualizerMath.Average(bands, 5, 7);
        var points = (int)Math.Ceiling(detail * (scene.Width + scene.Depth));
        for (var i = 0; i < points; i++)
        {
            var edge = VisualizerMath.PositiveModulo(frame.FrameNumber + i * 3L, 4);
            var p = VisualizerMath.Noise(frame.FrameNumber * 101 + i * 17);
            var x = edge < 2 ? VisualizerMath.Coord(p, scene.Width) : (edge == 2 ? 0 : scene.Width - 1);
            var y = edge >= 2 ? VisualizerMath.Coord(p, scene.Depth) : (edge == 0 ? 0 : scene.Depth - 1);
            var z = VisualizerMath.PositiveModulo(frame.FrameNumber + i * 5L, scene.Height);
            scene[x, y, z] = VisualizerMath.Color(6 + i % 2, frame.Capabilities, detail);
        }
    }
}

internal sealed class BassShockwaveVisualizer() : BeatVisualizerBase(
    "bass-shockwave", "Bass Shockwave", "Fast overlapping bass shells expand from a colorful reactive core.",
    VisualizerCategory.Beat, new VisualizerResponse(.95, 1.05, .02))
{
    private const int WaveCapacity = 6;
    private readonly Shockwave[] _waves = new Shockwave[WaveCapacity];
    private int _nextWave;
    private int _colorSequence;
    public override int StateCapacity => WaveCapacity + 3;

    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        var hit = UpdateBeat(audio, frame, .45);
        var bass = VisualizerMath.Average(bands, 0, 1);
        if (hit || (bass > .58 && PulseAge < frame.Delta.TotalSeconds * 1.5))
        {
            var dominant = VisualizerMath.DominantBand(bands);
            _waves[_nextWave] = new Shockwave
            {
                Age = 0,
                Duration = .32 + (1 - bass) * .16,
                Band = (dominant + _colorSequence++ * 2) % 8,
                Strength = Math.Max(.55, bass)
            };
            _nextWave = (_nextWave + 1) % _waves.Length;
        }

        if (bass > .02)
        {
            var radius = Math.Clamp(.08 + bass * .26, .08, .36);
            for (var x = 0; x < scene.Width; x++)
            for (var y = 0; y < scene.Depth; y++)
            for (var z = 0; z < scene.Height; z++)
                if (Math.Max(Math.Abs(VisualizerMath.Centered(x, scene.Width)),
                        Math.Max(Math.Abs(VisualizerMath.Centered(y, scene.Depth)),
                            Math.Abs(VisualizerMath.Centered(z, scene.Height)))) <= radius)
                    scene[x, y, z] = VisualizerMath.Color(
                        bass >= VisualizerMath.Average(bands, 2, 4) ? 0 : 4,
                        frame.Capabilities, bass);
        }

        var dt = Math.Clamp(frame.Delta.TotalSeconds, 0, .15);
        for (var i = 0; i < _waves.Length; i++)
        {
            ref var wave = ref _waves[i];
            if (wave.Duration <= 0 || wave.Age > wave.Duration) continue;
            var radius = Math.Clamp(wave.Age / wave.Duration, .04, 1);
            VisualizerGeometry.DrawBoxShell(scene, radius,
                VisualizerMath.Color(wave.Band, frame.Capabilities,
                    wave.Strength * (1 - radius * .35)));
            wave.Age += dt;
        }

        var detail = VisualizerMath.Average(bands, 5, 7);
        for (var i = 0; i < (int)Math.Ceiling(detail * 20); i++)
        {
            var seed = frame.FrameNumber * 97 + i * 131L;
            var x = VisualizerMath.Coord(VisualizerMath.Noise(seed), scene.Width);
            var y = VisualizerMath.Coord(VisualizerMath.Noise(seed + 1), scene.Depth);
            var z = VisualizerMath.Coord(VisualizerMath.Noise(seed + 2), scene.Height);
            scene[x, y, z] = VisualizerMath.Color(5 + i % 3, frame.Capabilities, detail);
        }
    }

    private struct Shockwave
    {
        public double Age, Duration, Strength;
        public int Band;
    }
}

internal sealed class SpectralRainVisualizer() : VisualizerBase(
    "spectral-rain", "Spectral Rain", "Frequency-colored droplets fall through the cube.",
    VisualizerCategory.Particles, new VisualizerResponse(1.55, .8, .01)), IBoundedVisualizerState
{
    private const int Capacity = 64;
    private readonly RainDrop[] _drops = new RainDrop[Capacity];
    private uint _randomState = 0x9e3779b9;
    public int StateCapacity => Capacity;

    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        for (var band = 0; band < 8; band++)
        {
            var spawnCount = bands[band] < .08 ? 0 : Math.Min(3, (int)Math.Ceiling(bands[band] * 2));
            for (var count = 0; count < spawnCount; count++) Spawn(band, bands[band]);
        }

        var dt = Math.Clamp(frame.Delta.TotalSeconds, 0, .1);
        for (var i = 0; i < _drops.Length; i++)
        {
            ref var drop = ref _drops[i];
            if (!drop.Active) continue;
            drop.Z -= dt * drop.Speed;
            if (drop.Z < 0) { drop.Active = false; continue; }
            scene.SetNormalized(drop.X, drop.Y, drop.Z,
                VisualizerMath.Color(drop.Band, frame.Capabilities, bands[drop.Band]));
        }

    }

    private void Spawn(int band, double energy)
    {
        for (var i = 0; i < _drops.Length; i++)
        {
            if (_drops[i].Active) continue;
            _drops[i] = new RainDrop
            {
                Active = true,
                Band = band,
                X = (band + .15 + Next() * .7) / 8,
                Y = Next(),
                Z = 1,
                Speed = .35 + energy * 1.25
            };
            return;
        }
    }

    private double Next()
    {
        _randomState ^= _randomState << 13;
        _randomState ^= _randomState >> 17;
        _randomState ^= _randomState << 5;
        return (_randomState & 0xffffff) / 16777215d;
    }

    private struct RainDrop
    {
        public bool Active;
        public int Band;
        public double X;
        public double Y;
        public double Z;
        public double Speed;
    }
}

internal sealed class SpectralPlasmaVisualizer() : VisualizerBase(
    "spectral-plasma", "Spectral Plasma", "Eight frequency sources blend into a flowing volumetric field.",
    VisualizerCategory.Volume, new VisualizerResponse(.5, .7))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        var phase = frame.Elapsed.TotalSeconds;
        for (var x = 0; x < scene.Width; x++)
        for (var y = 0; y < scene.Depth; y++)
        for (var z = 0; z < scene.Height; z++)
        {
            var nx = VisualizerMath.Unit(x, scene.Width);
            var ny = VisualizerMath.Unit(y, scene.Depth);
            var nz = VisualizerMath.Unit(z, scene.Height);
            var field = 0d;
            var strongest = 0;
            var strongestContribution = 0d;
            for (var band = 0; band < 8; band++)
            {
                var sx = (band & 1) == 0 ? .15 : .85;
                var sy = (band & 2) == 0 ? .15 : .85;
                var sz = (band & 4) == 0 ? .15 : .85;
                var dx = nx - sx; var dy = ny - sy; var dz = nz - sz;
                var contribution = bands[band] / (1 + (dx * dx + dy * dy + dz * dz) * 10);
                field += contribution;
                if (contribution > strongestContribution) { strongestContribution = contribution; strongest = band; }
            }
            var averageContribution = field / 8;
            var averageEnergy = VisualizerMath.Average(bands, 0, 7);
            var wave = .15 * averageEnergy * Math.Sin((nx + ny + nz) * Math.Tau + phase * 2);
            var plasmaField = strongestContribution + averageContribution * .5 + wave;
            if (plasmaField > .09)
                scene[x, y, z] = VisualizerMath.Color(strongest, frame.Capabilities,
                    Math.Min(1, plasmaField / .2));
        }
    }
}

internal sealed class TriBandWaveSpaceVisualizer() : VisualizerBase(
    "tri-band-wave-space", "Tri-Band Wave Space", "Three audio bands travel as interlaced waves through the volume.",
    VisualizerCategory.Spatial, new VisualizerResponse(1.15, .9, .01))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        for (var group = 0; group < 3; group++)
        {
            var level = group == 0 ? VisualizerMath.Average(bands, 0, 1) :
                group == 1 ? VisualizerMath.Average(bands, 2, 4) : VisualizerMath.Average(bands, 5, 7);
            if (level <= .02) continue;
            var color = VisualizerMath.Color(group * 3, frame.Capabilities, level);
            for (var x = 0; x < scene.Width; x++)
            {
                var t = VisualizerMath.Unit(x, scene.Width);
                var y = .5 + Math.Sin(t * Math.Tau * (group + 1) + frame.Elapsed.TotalSeconds * (1.2 + group)) * .42;
                var z = .5 + Math.Cos(t * Math.Tau + frame.Elapsed.TotalSeconds * (1.5 + group) + group * 2) * level * .45;
                scene.SetNormalized(t, y, z, color);
            }
        }
    }
}

internal sealed class FrequencyFirestormVisualizer() : VisualizerBase(
    "frequency-firestorm", "Frequency Firestorm", "Bass-fed flames with midrange tongues and treble embers.",
    VisualizerCategory.Particles, new VisualizerResponse(1.25, .88, .01))
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
            var noise = VisualizerMath.Noise(frame.FrameNumber / 2 + x * 131L + y * 719L);
            var level = Math.Clamp(bass * .65 + mids * .35 + (noise - .5) * .22, 0, 1);
            var height = VisualizerMath.Height(level, scene.Height);
            for (var z = 0; z < height; z++)
                scene[x, y, z] = VisualizerMath.Color(z * 5 / Math.Max(1, scene.Height), frame.Capabilities, level);
        }
        var embers = (int)Math.Ceiling(highs * 24);
        for (var i = 0; i < embers; i++)
        {
            var seed = frame.FrameNumber * 47 + i * 101L;
            scene[VisualizerMath.PositiveModulo(seed, scene.Width),
                VisualizerMath.PositiveModulo(seed / 3, scene.Depth),
                VisualizerMath.PositiveModulo(seed / 7, scene.Height)] =
                VisualizerMath.Color(7, frame.Capabilities, highs);
        }
    }
}
