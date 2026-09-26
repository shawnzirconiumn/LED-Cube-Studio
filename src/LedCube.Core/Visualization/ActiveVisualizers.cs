using LedCube.Core.Audio;
using LedCube.Core.Scene;

namespace LedCube.Core.Visualization;

internal abstract class ActiveParticleVisualizerBase(
    string id, string name, string description, VisualizerCategory category,
    VisualizerResponse response, int capacity, uint seed)
    : BeatVisualizerBase(id, name, description, category, response)
{
    protected readonly ActiveParticle[] Particles = new ActiveParticle[capacity];
    private uint _randomState = seed;
    public override int StateCapacity => Particles.Length + 4;

    protected double Next()
    {
        _randomState ^= _randomState << 13;
        _randomState ^= _randomState >> 17;
        _randomState ^= _randomState << 5;
        return (_randomState & 0xffffff) / 16777215d;
    }

    protected void DrawParticles(VoxelScene scene, VisualizationFrame frame, double gravity = 0)
    {
        var dt = Math.Clamp(frame.Delta.TotalSeconds, 0, .15);
        for (var i = 0; i < Particles.Length; i++)
        {
            ref var particle = ref Particles[i];
            if (particle.Life <= 0) continue;
            var oldX = particle.X; var oldY = particle.Y; var oldZ = particle.Z;
            particle.X += particle.Vx * dt;
            particle.Y += particle.Vy * dt;
            particle.Z += particle.Vz * dt;
            particle.Vz -= gravity * dt;
            particle.Life -= dt;
            if (particle.Life <= 0 || particle.X is < 0 or > 1 ||
                particle.Y is < 0 or > 1 || particle.Z is < 0 or > 1)
            {
                particle.Life = 0;
                continue;
            }
            var color = VisualizerMath.Color(particle.Band, frame.Capabilities,
                Math.Clamp(particle.Life / Math.Max(.1, particle.MaxLife), .1, 1));
            VisualizerGeometry.DrawLine(scene, oldX, oldY, oldZ,
                particle.X, particle.Y, particle.Z, color);
        }
    }

    protected struct ActiveParticle
    {
        public double X, Y, Z, Vx, Vy, Vz, Life, MaxLife;
        public int Band;
    }
}

internal sealed class RgbSupernovaVisualizer() : BeatVisualizerBase(
    "rgb-supernova", "RGB Supernova", "Fast multicolor shells and sparks erupt from a beat-lit core.",
    VisualizerCategory.Beat, new VisualizerResponse(1.05, .9, .01))
{
    private readonly Wave[] _waves = new Wave[6];
    private int _generation;
    public override int StateCapacity => _waves.Length + 3;

    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        if (UpdateBeat(audio, frame, .42))
        {
            var dominant = VisualizerMath.DominantBand(bands);
            var maxRadius = Math.Clamp(.34 + bands[dominant] * .78, .38, 1);
            for (var i = 0; i < 3; i++)
                _waves[(_generation * 3 + i) % _waves.Length] =
                    new Wave
                    {
                        Active = true, Age = -i * .055,
                        Band = (dominant + i * 2 + _generation) % 8,
                        MaxRadius = Math.Clamp(maxRadius - i * .08, .3, 1)
                    };
            _generation++;
        }
        var energy = VisualizerMath.Average(bands, 0, 7);
        if (energy > .02)
            VisualizerGeometry.DrawBoxShell(scene, .08 + energy * .18,
                VisualizerMath.Color(7, frame.Capabilities, energy));
        var dt = Math.Clamp(frame.Delta.TotalSeconds, 0, .15);
        for (var i = 0; i < _waves.Length; i++)
        {
            ref var wave = ref _waves[i];
            if (!wave.Active) continue;
            wave.Age += dt;
            if (wave.Age > .48) { wave.Active = false; continue; }
            if (wave.Age < 0) continue;
            var radius = wave.Age / .45 * wave.MaxRadius;
            VisualizerGeometry.DrawBoxShell(scene, radius,
                VisualizerMath.Color(wave.Band, frame.Capabilities, 1 - radius * .3));
            var sparks = (int)Math.Ceiling(VisualizerMath.Average(bands, 5, 7) * 12);
            for (var spark = 0; spark < sparks; spark++)
            {
                var seed = (_generation + 1) * 1009L + i * 131 + spark * 17L;
                scene.SetNormalized(.5 + (VisualizerMath.Noise(seed) - .5) * radius,
                    .5 + (VisualizerMath.Noise(seed + 1) - .5) * radius,
                    .5 + (VisualizerMath.Noise(seed + 2) - .5) * radius,
                    VisualizerMath.Color(5 + spark % 3, frame.Capabilities, energy));
            }
        }
    }

    private struct Wave { public bool Active; public double Age, MaxRadius; public int Band; }
}

internal sealed class BeatFireworksVisualizer() : ActiveParticleVisualizerBase(
    "beat-fireworks", "Beat Fireworks", "Beats burst from changing 3D launch points with spectral trails.",
    VisualizerCategory.Particles, new VisualizerResponse(1.1, .9, .01), 96, 0x13579bdf)
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        if (UpdateBeat(audio, frame, .42)) Launch(bands);
        DrawParticles(scene, frame, .28);
    }

    private void Launch(ReadOnlySpan<double> bands)
    {
        var ox = .15 + Next() * .7; var oy = .15 + Next() * .7; var oz = .25 + Next() * .55;
        for (var i = 0; i < Particles.Length; i++)
        {
            var band = i % 8;
            var azimuth = Next() * Math.Tau;
            var elevation = Next() * 2 - 1;
            var radial = Math.Sqrt(Math.Max(0, 1 - elevation * elevation));
            var speed = .35 + bands[band] * .9;
            Particles[i] = new ActiveParticle
            {
                X = ox, Y = oy, Z = oz,
                Vx = Math.Cos(azimuth) * radial * speed,
                Vy = Math.Sin(azimuth) * radial * speed,
                Vz = elevation * speed + .15,
                Life = .45 + bands[band] * .55, MaxLife = 1, Band = band
            };
        }
    }
}

internal sealed class SpectralCrossfireVisualizer() : VisualizerBase(
    "spectral-crossfire", "Spectral Crossfire", "Bass, mids, and highs collide inward from all six faces.",
    VisualizerCategory.Spatial, new VisualizerResponse(1.05, .92, .01))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        Span<double> groups = stackalloc double[3];
        groups[0] = VisualizerMath.Average(bands, 0, 1);
        groups[1] = VisualizerMath.Average(bands, 2, 4);
        groups[2] = VisualizerMath.Average(bands, 5, 7);
        var phase = frame.Elapsed.TotalSeconds * 3.2;
        for (var axis = 0; axis < 3; axis++)
        {
            var level = groups[axis];
            if (level <= .02) continue;
            var travel = (phase * (.75 + level) + axis * .27) % 1;
            var a = travel * .5; var b = 1 - a;
            var streaks = Math.Max(2, (int)Math.Ceiling(level * 8));
            for (var i = 0; i < streaks; i++)
            {
                var p = (i + .5) / streaks;
                var q = (p + axis * .31 + phase * .08) % 1;
                var color = VisualizerMath.Color(axis * 3, frame.Capabilities, level);
                if (axis == 0) { scene.SetNormalized(a, p, q, color); scene.SetNormalized(b, 1 - p, q, color); }
                else if (axis == 1) { scene.SetNormalized(p, a, q, color); scene.SetNormalized(1 - p, b, q, color); }
                else { scene.SetNormalized(p, q, a, color); scene.SetNormalized(1 - p, q, b, color); }
            }
        }
    }
}

internal sealed class FrequencyStormVisualizer() : BeatVisualizerBase(
    "frequency-storm", "Frequency Storm", "Bass flashes, midrange streaks, and treble sparks churn through the full volume.",
    VisualizerCategory.Volume, new VisualizerResponse(1.05, .9, .01))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        var hit = UpdateBeat(audio, frame, .45);
        var bass = VisualizerMath.Average(bands, 0, 1);
        var mids = VisualizerMath.Average(bands, 2, 4);
        var highs = VisualizerMath.Average(bands, 5, 7);
        if (hit || PulseAge < .14)
            VisualizerGeometry.DrawBoxShell(scene, 1,
                VisualizerMath.Color((int)(frame.FrameNumber % 3), frame.Capabilities, bass));
        var streaks = (int)Math.Ceiling(mids * 18);
        for (var i = 0; i < streaks; i++)
        {
            var seed = frame.FrameNumber * 53 + i * 101L;
            var x = VisualizerMath.Noise(seed); var y = VisualizerMath.Noise(seed + 1);
            var z = (VisualizerMath.Noise(seed + 2) + frame.Elapsed.TotalSeconds * 2.5) % 1;
            VisualizerGeometry.DrawLine(scene, x, y, z, 1 - x, 1 - y,
                Math.Clamp(z + .18, 0, 1), VisualizerMath.Color(3 + i % 2, frame.Capabilities, mids));
        }
        for (var i = 0; i < (int)Math.Ceiling(highs * 36); i++)
        {
            var seed = frame.FrameNumber * 997 + i * 37L;
            scene.SetNormalized(VisualizerMath.Noise(seed), VisualizerMath.Noise(seed + 1),
                VisualizerMath.Noise(seed + 2), VisualizerMath.Color(5 + i % 3, frame.Capabilities, highs));
        }
    }
}

internal sealed class BeatCometsVisualizer() : ActiveParticleVisualizerBase(
    "beat-comets", "Beat Comets", "Beats launch colorful comets along fast crossing 3D paths.",
    VisualizerCategory.Particles, new VisualizerResponse(1.05, .9, .01), 64, 0x2468ace1)
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        if (UpdateBeat(audio, frame, .44)) Launch(bands);
        DrawParticles(scene, frame);
    }

    private void Launch(ReadOnlySpan<double> bands)
    {
        var energy = VisualizerMath.Average(bands, 0, 7);
        var activeCount = Math.Clamp(12 + (int)Math.Ceiling(energy * 80), 12, Particles.Length);
        for (var i = 0; i < Particles.Length; i++)
        {
            if (i >= activeCount) { Particles[i].Life = 0; continue; }
            var band = i % 8;
            var axis = i % 3;
            var direction = (i & 1) == 0 ? 1d : -1d;
            var speed = .7 + bands[band] * 1.2;
            var p = Next(); var q = Next();
            Particles[i] = new ActiveParticle
            {
                X = axis == 0 ? (direction > 0 ? 0 : 1) : p,
                Y = axis == 1 ? (direction > 0 ? 0 : 1) : q,
                Z = axis == 2 ? (direction > 0 ? 0 : 1) : (p + q) % 1,
                Vx = axis == 0 ? direction * speed : (Next() - .5) * .25,
                Vy = axis == 1 ? direction * speed : (Next() - .5) * .25,
                Vz = axis == 2 ? direction * speed : (Next() - .5) * .25,
                Life = .5, MaxLife = .5, Band = band
            };
        }
    }
}

internal sealed class RgbFloorquakeVisualizer() : BeatVisualizerBase(
    "rgb-floorquake", "RGB Floorquake", "Bass waves race upward while mid ripples and high sparks break away.",
    VisualizerCategory.Beat, new VisualizerResponse(1.05, .92, .01))
{
    private readonly RisingWave[] _waves = new RisingWave[4];
    private int _next;
    public override int StateCapacity => _waves.Length + 3;

    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        if (UpdateBeat(audio, frame, .43))
        {
            _waves[_next] = new RisingWave { Active = true, Age = 0, Band = VisualizerMath.DominantBand(bands) };
            _next = (_next + 1) % _waves.Length;
        }
        var mids = VisualizerMath.Average(bands, 2, 4);
        var dt = Math.Clamp(frame.Delta.TotalSeconds, 0, .15);
        for (var i = 0; i < _waves.Length; i++)
        {
            ref var wave = ref _waves[i];
            if (!wave.Active) continue;
            if (wave.Age > .55) { wave.Active = false; continue; }
            var z = wave.Age / .5;
            var wobble = Math.Sin(frame.Elapsed.TotalSeconds * 9 + i) * mids * .16;
            VisualizerGeometry.DrawSquareRing(scene, Math.Clamp(.25 + z * .75 + wobble, .1, 1), z,
                VisualizerMath.Color(wave.Band, frame.Capabilities, 1 - z * .25));
            wave.Age += dt;
        }
        var highs = VisualizerMath.Average(bands, 5, 7);
        for (var i = 0; i < (int)Math.Ceiling(highs * 20); i++)
        {
            var seed = frame.FrameNumber * 79 + i * 41L;
            scene.SetNormalized(VisualizerMath.Noise(seed), VisualizerMath.Noise(seed + 1),
                (frame.Elapsed.TotalSeconds * 2 + VisualizerMath.Noise(seed + 2)) % 1,
                VisualizerMath.Color(5 + i % 3, frame.Capabilities, highs));
        }
    }

    private struct RisingWave { public bool Active; public double Age; public int Band; }
}

internal sealed class SpectralImplosionExplosionVisualizer() : BeatVisualizerBase(
    "spectral-implosion-explosion", "Spectral Implosion / Explosion",
    "Alternating beats collapse outer shells inward and blast center shells outward.",
    VisualizerCategory.Beat, new VisualizerResponse(1.05, .9, .01))
{
    private readonly DirectionalWave[] _waves = new DirectionalWave[6];
    private int _next; private bool _implode;
    public override int StateCapacity => _waves.Length + 4;

    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        if (UpdateBeat(audio, frame, .43))
        {
            _waves[_next] = new DirectionalWave
            {
                Active = true, Age = 0, Implode = _implode = !_implode,
                Band = (VisualizerMath.DominantBand(bands) + _next) % 8
            };
            _next = (_next + 1) % _waves.Length;
        }
        var dt = Math.Clamp(frame.Delta.TotalSeconds, 0, .15);
        for (var i = 0; i < _waves.Length; i++)
        {
            ref var wave = ref _waves[i];
            if (!wave.Active) continue;
            if (wave.Age > .5) { wave.Active = false; continue; }
            var progress = wave.Age / .46;
            var radius = wave.Implode ? 1 - progress : progress;
            VisualizerGeometry.DrawBoxShell(scene, Math.Clamp(radius, .04, 1),
                VisualizerMath.Color(wave.Band, frame.Capabilities, 1 - progress * .3));
            wave.Age += dt;
        }
    }

    private struct DirectionalWave { public bool Active; public double Age; public bool Implode; public int Band; }
}

internal sealed class AudioFireVisualizer() : VisualizerBase(
    "audio-fire", "Audio Fire", "Fast multicolor flame masses and sparks rise through the volume.",
    VisualizerCategory.Volume, new VisualizerResponse(1.05, .88, .01))
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
            var seed = x * 131L + y * 719L;
            var flicker = VisualizerMath.Noise(seed + (long)(frame.Elapsed.TotalSeconds * 24));
            var level = Math.Clamp(bass * .65 + mids * .45 + (flicker - .5) * .28, 0, 1);
            var height = VisualizerMath.Height(level, scene.Height, .015);
            for (var z = 0; z < height; z++)
            {
                var band = z < height / 2 ? (x + y) % 3 : 3 + (x + z) % 2;
                scene[x, y, z] = VisualizerMath.Color(band, frame.Capabilities, level);
            }
        }
        for (var i = 0; i < (int)Math.Ceiling(highs * 28); i++)
        {
            var seed = frame.FrameNumber * 83 + i * 149L;
            scene.SetNormalized(VisualizerMath.Noise(seed), VisualizerMath.Noise(seed + 1),
                (VisualizerMath.Noise(seed + 2) + frame.Elapsed.TotalSeconds * 3.5) % 1,
                VisualizerMath.Color(5 + i % 3, frame.Capabilities, highs));
        }
    }
}

internal sealed class BeatCubesVisualizer() : BeatVisualizerBase(
    "beat-cubes", "Beat Cubes", "Overlapping nested cubes rapidly expand and contract in spectral colors.",
    VisualizerCategory.Beat, new VisualizerResponse(1.05, .9, .01))
{
    private readonly CubePulse[] _pulses = new CubePulse[8];
    private int _next;
    public override int StateCapacity => _pulses.Length + 3;

    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        if (UpdateBeat(audio, frame, .42))
            for (var i = 0; i < 3; i++)
            {
                _pulses[_next] = new CubePulse
                {
                    Active = true, Age = -i * .05, Band = (VisualizerMath.DominantBand(bands) + i * 2) % 8,
                    Contract = ((_next + i) & 1) != 0,
                    MaxRadius = Math.Clamp(.34 + bands[VisualizerMath.DominantBand(bands)] * .78 - i * .06, .3, 1)
                };
                _next = (_next + 1) % _pulses.Length;
            }
        var dt = Math.Clamp(frame.Delta.TotalSeconds, 0, .15);
        for (var i = 0; i < _pulses.Length; i++)
        {
            ref var pulse = ref _pulses[i];
            if (!pulse.Active) continue;
            pulse.Age += dt;
            if (pulse.Age > .52) { pulse.Active = false; continue; }
            if (pulse.Age < 0) continue;
            var progress = pulse.Age / .48;
            var radius = pulse.Contract
                ? pulse.MaxRadius * (1 - progress)
                : pulse.MaxRadius * progress;
            VisualizerGeometry.DrawBoxShell(scene, Math.Clamp(radius, .06, 1),
                VisualizerMath.Color(pulse.Band, frame.Capabilities, 1 - progress * .3));
        }
    }

    private struct CubePulse
    {
        public bool Active, Contract;
        public double Age, MaxRadius;
        public int Band;
    }
}

internal sealed class SpectrumWaves3DVisualizer() : VisualizerBase(
    "spectrum-waves-3d", "Spectrum Waves 3D", "Simultaneous bass, mid, and high waves race along all three axes.",
    VisualizerCategory.Spatial, new VisualizerResponse(1.05, .9, .01))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        Span<double> levels = stackalloc double[3];
        levels[0] = VisualizerMath.Average(bands, 0, 1);
        levels[1] = VisualizerMath.Average(bands, 2, 4);
        levels[2] = VisualizerMath.Average(bands, 5, 7);
        var time = frame.Elapsed.TotalSeconds * 4;
        for (var x = 0; x < scene.Width; x++)
        for (var y = 0; y < scene.Depth; y++)
        for (var z = 0; z < scene.Height; z++)
        {
            var nx = VisualizerMath.Unit(x, scene.Width);
            var ny = VisualizerMath.Unit(y, scene.Depth);
            var nz = VisualizerMath.Unit(z, scene.Height);
            var wx = .5 + .5 * Math.Sin((nx * 2 + time) * Math.Tau);
            var wy = .5 + .5 * Math.Sin((ny * 2 - time * 1.17) * Math.Tau);
            var wz = .5 + .5 * Math.Sin((nz * 2 + time * 1.33) * Math.Tau);
            var sx = levels[0] * wx; var sy = levels[1] * wy; var sz = levels[2] * wz;
            var strongest = sx >= sy && sx >= sz ? 0 : sy >= sz ? 3 : 6;
            if (Math.Max(sx, Math.Max(sy, sz)) > .16)
                scene[x, y, z] = VisualizerMath.Color(strongest, frame.Capabilities,
                    Math.Max(sx, Math.Max(sy, sz)));
        }
    }
}

internal sealed class SpectralParticleBurstVisualizer() : ActiveParticleVisualizerBase(
    "spectral-particle-burst", "Spectral Particle Burst",
    "A fixed spectral particle cloud explodes and redirects on every beat.",
    VisualizerCategory.Particles, new VisualizerResponse(1.05, .88, .01), 96, 0xdeadbeef)
{
    private bool _initialized;

    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        if (!_initialized) Initialize(bands);
        var hit = UpdateBeat(audio, frame, .42);
        if (hit)
        {
            for (var i = 0; i < Particles.Length; i++)
            {
                ref var p = ref Particles[i];
                var dx = p.X - .5; var dy = p.Y - .5; var dz = p.Z - .5;
                var length = Math.Sqrt(dx * dx + dy * dy + dz * dz) + .01;
                var boost = .45 + bands[p.Band] * 1.1;
                p.Vx = dx / length * boost + (Next() - .5) * .35;
                p.Vy = dy / length * boost + (Next() - .5) * .35;
                p.Vz = dz / length * boost + (Next() - .5) * .35;
                p.Life = p.MaxLife = .8;
            }
        }
        DrawParticles(scene, frame);
        var activeCount = Math.Clamp(16 + (int)Math.Ceiling(
            VisualizerMath.Average(bands, 0, 7) * 120), 16, Particles.Length);
        for (var i = 0; i < Particles.Length; i++)
        {
            ref var p = ref Particles[i];
            if (i >= activeCount) { p.Life = 0; continue; }
            if (p.Life > 0) continue;
            p.X = .2 + Next() * .6; p.Y = .2 + Next() * .6; p.Z = .2 + Next() * .6;
            p.Vx = p.Vy = p.Vz = 0; p.Life = p.MaxLife = .3 + bands[p.Band] * .4;
        }
    }

    private void Initialize(ReadOnlySpan<double> bands)
    {
        _initialized = true;
        var activeCount = Math.Clamp(16 + (int)Math.Ceiling(
            VisualizerMath.Average(bands, 0, 7) * 120), 16, Particles.Length);
        for (var i = 0; i < activeCount; i++)
            Particles[i] = new ActiveParticle
            {
                X = Next(), Y = Next(), Z = Next(), Band = i % 8,
                Life = .3 + bands[i % 8] * .4, MaxLife = .7
            };
    }
}

internal sealed class RgbPulseGridVisualizer() : BeatVisualizerBase(
    "rgb-pulse-grid", "RGB Pulse Grid", "Bass, mid, and high pulses race independently along a 3D lattice.",
    VisualizerCategory.Spatial, new VisualizerResponse(1.05, .9, .01))
{
    private readonly GridPulse[] _pulses = new GridPulse[9];
    private int _next;
    public override int StateCapacity => _pulses.Length + 3;

    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        if (UpdateBeat(audio, frame, .43))
            for (var axis = 0; axis < 3; axis++)
            {
                _pulses[_next] = new GridPulse { Active = true, Age = 0, Axis = axis, Band = axis * 3 };
                _next = (_next + 1) % _pulses.Length;
            }
        var dt = Math.Clamp(frame.Delta.TotalSeconds, 0, .15);
        for (var i = 0; i < _pulses.Length; i++)
        {
            ref var pulse = ref _pulses[i];
            if (!pulse.Active) continue;
            if (pulse.Age > .55) { pulse.Active = false; continue; }
            var position = pulse.Age / .5;
            var color = VisualizerMath.Color(pulse.Band, frame.Capabilities,
                1 - position * .25);
            var stride = Math.Max(1, Math.Min(scene.Width, Math.Min(scene.Depth, scene.Height)) / 3);
            if (pulse.Axis == 0)
            {
                var x = VisualizerMath.Coord(position, scene.Width);
                for (var y = 0; y < scene.Depth; y += stride)
                for (var z = 0; z < scene.Height; z += stride) scene[x, y, z] = color;
            }
            else if (pulse.Axis == 1)
            {
                var y = VisualizerMath.Coord(position, scene.Depth);
                for (var x = 0; x < scene.Width; x += stride)
                for (var z = 0; z < scene.Height; z += stride) scene[x, y, z] = color;
            }
            else
            {
                var z = VisualizerMath.Coord(position, scene.Height);
                for (var x = 0; x < scene.Width; x += stride)
                for (var y = 0; y < scene.Depth; y += stride) scene[x, y, z] = color;
            }
            pulse.Age += dt;
        }
    }

    private struct GridPulse { public bool Active; public double Age; public int Axis, Band; }
}
