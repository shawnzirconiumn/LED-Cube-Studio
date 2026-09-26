using LedCube.Core.Audio;
using LedCube.Core.Scene;

namespace LedCube.Core.Visualization;

internal sealed class BeatExplosionVisualizer() : BeatVisualizerBase(
    "beat-explosion", "Beat Explosion", "Each beat launches a fixed pool of colorful particles from the center.",
    VisualizerCategory.Beat, new VisualizerResponse(1.15, .9, .01)), IBoundedVisualizerState
{
    private const int ParticleCapacity = 72;
    private readonly Particle[] _particles = new Particle[ParticleCapacity];
    public override int StateCapacity => ParticleCapacity + 2;

    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        if (UpdateBeat(audio, frame, .52)) Launch(bands);
        var dt = Math.Clamp(frame.Delta.TotalSeconds, 0, .1);
        for (var i = 0; i < _particles.Length; i++)
        {
            ref var particle = ref _particles[i];
            if (particle.Life <= 0) continue;
            particle.X += particle.Vx * dt;
            particle.Y += particle.Vy * dt;
            particle.Z += particle.Vz * dt;
            particle.Vz -= dt * .35;
            particle.Life -= dt;
            if (particle.Life <= 0 || particle.X is < 0 or > 1 || particle.Y is < 0 or > 1 || particle.Z is < 0 or > 1)
                continue;
            scene.SetNormalized(particle.X, particle.Y, particle.Z,
                VisualizerMath.Color(particle.Band, frame.Capabilities, particle.Life));
        }
    }

    private void Launch(ReadOnlySpan<double> bands)
    {
        for (var i = 0; i < _particles.Length; i++)
        {
            var band = i % 8;
            var azimuth = i * 2.3999632297;
            var z = -1 + 2 * ((i + .5) / _particles.Length);
            var radial = Math.Sqrt(Math.Max(0, 1 - z * z));
            var speed = .3 + bands[band] * .85;
            _particles[i] = new Particle
            {
                X = .5, Y = .5, Z = .5,
                Vx = Math.Cos(azimuth) * radial * speed,
                Vy = Math.Sin(azimuth) * radial * speed,
                Vz = z * speed,
                Life = .65 + bands[band] * .55,
                Band = band
            };
        }
    }

    private struct Particle
    {
        public double X, Y, Z, Vx, Vy, Vz, Life;
        public int Band;
    }
}

internal sealed class SpectralSupernovaVisualizer() : BeatVisualizerBase(
    "spectral-supernova", "Spectral Supernova", "A beat ignites a bright core, expanding shell, and spectral rays.",
    VisualizerCategory.Beat, new VisualizerResponse(1, .95, .02))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        UpdateBeat(audio, frame, .48);
        var total = VisualizerMath.Average(bands, 0, 7);
        if (total > .02 && PulseAge > .12)
            VisualizerGeometry.DrawBoxShell(scene, Math.Min(.28, .06 + total * .22),
                VisualizerMath.Color(2, frame.Capabilities, total));
        if (PulseAge < 1.1)
        {
            var radius = Math.Clamp(PulseAge / 1.05, .04, 1);
            VisualizerGeometry.DrawBoxShell(scene, radius,
                VisualizerMath.Color(Math.Min(7, (int)(radius * 8)), frame.Capabilities, 1 - radius * .6));
            var rays = Math.Max(8, (int)Math.Ceiling(total * 32));
            for (var i = 0; i < rays; i++)
            {
                var seed = i * 7919L;
                var dx = VisualizerMath.Noise(seed) * 2 - 1;
                var dy = VisualizerMath.Noise(seed + 1) * 2 - 1;
                var dz = VisualizerMath.Noise(seed + 2) * 2 - 1;
                var scale = radius * .5;
                VisualizerGeometry.DrawLine(scene, .5, .5, .5,
                    .5 + dx * scale, .5 + dy * scale, .5 + dz * scale,
                    VisualizerMath.Color(i % 8, frame.Capabilities, total));
            }
        }
    }
}

internal sealed class FrequencyShockwavesVisualizer() : HistoryVisualizerBase(
    "frequency-shockwaves", "Frequency Shockwaves", "Recent frequency energy travels outward as nested shells.",
    VisualizerCategory.Beat, new VisualizerResponse(.85, 1.65, .035))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        PushHistory(bands);
        var shells = Math.Min(Math.Min(scene.Width, Math.Min(scene.Depth, scene.Height)) / 2 + 1, 8);
        for (var shell = 0; shell < shells; shell++)
        {
            var age = Math.Min(HistoryLength - 1, shell * 3);
            var energySquared = 0d;
            var strongestBand = 0;
            for (var band = 0; band < 8; band++)
            {
                var bandEnergy = History[band, age];
                energySquared += bandEnergy * bandEnergy;
                if (bandEnergy > History[strongestBand, age]) strongestBand = band;
            }
            var energy = Math.Sqrt(energySquared / 8) * (1 - shell * .08);
            var activationThreshold = .005 + shell * .035;
            if (energy <= activationThreshold) continue;
            VisualizerGeometry.DrawBoxShell(scene, (shell + 1d) / shells,
                VisualizerMath.Color(strongestBand, frame.Capabilities, energy));
        }
    }
}

internal sealed class SpectralLightningStormVisualizer() : BeatVisualizerBase(
    "spectral-lightning-storm", "Spectral Lightning Storm", "Treble forks into bolts while beats flash across the volume.",
    VisualizerCategory.Beat, new VisualizerResponse(1.25, .9, .01))
{
    protected override void RenderCore(VoxelScene scene, ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        var hit = UpdateBeat(audio, frame, .5);
        var highs = VisualizerMath.Average(bands, 5, 7);
        var boltCount = Math.Min(8, (int)Math.Ceiling(highs * 6) + (hit ? 2 : 0));
        for (var bolt = 0; bolt < boltCount; bolt++)
        {
            var seed = frame.FrameNumber / 2 * 997 + bolt * 131L;
            var lastX = VisualizerMath.Noise(seed);
            var lastY = VisualizerMath.Noise(seed + 1);
            for (var segment = 1; segment <= Math.Max(3, scene.Height); segment++)
            {
                var z0 = 1 - (segment - 1d) / Math.Max(3, scene.Height);
                var z1 = 1 - segment / (double)Math.Max(3, scene.Height);
                var x = Math.Clamp(lastX + (VisualizerMath.Noise(seed + segment * 2) - .5) * .28, 0, 1);
                var y = Math.Clamp(lastY + (VisualizerMath.Noise(seed + segment * 2 + 1) - .5) * .28, 0, 1);
                VisualizerGeometry.DrawLine(scene, lastX, lastY, z0, x, y, z1,
                    VisualizerMath.Color(6 + bolt % 2, frame.Capabilities, Math.Max(highs, .45)));
                lastX = x; lastY = y;
            }
        }
        if (PulseAge < .12)
            VisualizerGeometry.DrawBoxShell(scene, 1, VisualizerMath.Color(7, frame.Capabilities, 1));
    }
}
