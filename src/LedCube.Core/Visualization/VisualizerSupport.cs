using LedCube.Core.Audio;
using LedCube.Core.Devices;
using LedCube.Core.Scene;

namespace LedCube.Core.Visualization;

internal readonly record struct VisualizerResponse(double Gain = 1, double Gamma = 1, double Threshold = 0)
{
    public double Apply(double value)
    {
        var aboveThreshold = Math.Max(0, value - Threshold);
        return Math.Min(1, Gain * Math.Pow(aboveThreshold, Gamma));
    }
}

internal abstract class VisualizerBase(
    string id,
    string displayName,
    string description,
    VisualizerCategory category,
    VisualizerResponse response = default) : IVisualizer
{
    private readonly VisualizerResponse _response = response == default ? new VisualizerResponse(1, 1, 0) : response;

    public string Id { get; } = id;
    public string DisplayName { get; } = displayName;
    public string Description { get; } = description;
    public VisualizerCategory Category { get; } = category;
    public DeviceCapabilities? RequiredCapability => null;

    public void Render(VoxelScene scene, AudioAnalysisSnapshot audio, VisualizationFrame frame)
    {
        Span<double> bands = stackalloc double[8];
        for (var band = 0; band < bands.Length; band++)
            bands[band] = _response.Apply(audio.Spectrum.Count > band ? audio.Spectrum[band] : 0);
        scene.Clear();
        RenderCore(scene, bands, audio, frame);
    }

    protected abstract void RenderCore(
        VoxelScene scene,
        ReadOnlySpan<double> bands,
        AudioAnalysisSnapshot audio,
        VisualizationFrame frame);
}

internal abstract class BeatVisualizerBase(
    string id,
    string displayName,
    string description,
    VisualizerCategory category,
    VisualizerResponse response) : VisualizerBase(id, displayName, description, category, response), IBoundedVisualizerState
{
    private double _previousEnvelope;
    private bool _previousBeat;
    protected double PulseAge { get; private set; } = 2;
    public virtual int StateCapacity => 2;

    protected bool UpdateBeat(AudioAnalysisSnapshot audio, VisualizationFrame frame, double trigger = .45)
    {
        var hit = (audio.IsBeat && !_previousBeat) ||
                  (audio.BeatEnvelope >= trigger && _previousEnvelope < trigger);
        _previousBeat = audio.IsBeat;
        _previousEnvelope = audio.BeatEnvelope;
        PulseAge = hit ? 0 : PulseAge + Math.Max(0, frame.Delta.TotalSeconds);
        return hit;
    }
}

internal abstract class HistoryVisualizerBase(
    string id,
    string displayName,
    string description,
    VisualizerCategory category,
    VisualizerResponse response) : VisualizerBase(id, displayName, description, category, response), IBoundedVisualizerState
{
    protected const int HistoryLength = 64;
    protected readonly double[,] History = new double[8, HistoryLength];
    public int StateCapacity => 8 * HistoryLength;

    protected void PushHistory(ReadOnlySpan<double> bands)
    {
        for (var band = 0; band < 8; band++)
        {
            for (var age = HistoryLength - 1; age > 0; age--)
                History[band, age] = History[band, age - 1];
            History[band, 0] = bands[band];
        }
    }
}

internal static class VisualizerMath
{
    public static int Height(double level, int size, double threshold = .02) =>
        level <= threshold ? 0 : Math.Clamp((int)Math.Ceiling(level * size), 1, size);

    public static double Average(ReadOnlySpan<double> bands, int first, int last)
    {
        var sum = 0d;
        for (var i = first; i <= last; i++) sum += bands[i];
        return sum / (last - first + 1);
    }

    public static int DominantBand(ReadOnlySpan<double> bands)
    {
        var strongest = 0;
        for (var band = 1; band < bands.Length; band++)
            if (bands[band] > bands[strongest]) strongest = band;
        return strongest;
    }

    public static double Unit(int coordinate, int size) => size <= 1 ? .5 : coordinate / (double)(size - 1);
    public static double Centered(int coordinate, int size) => Unit(coordinate, size) * 2 - 1;
    public static int Coord(double normalized, int size) => VoxelScene.ScaleCoordinate(normalized, size);
    public static int PositiveModulo(long value, int modulus) => (int)((value % modulus + modulus) % modulus);

    public static double Noise(long seed)
    {
        unchecked
        {
            var value = (ulong)seed;
            value ^= value >> 33;
            value *= 0xff51afd7ed558ccdUL;
            value ^= value >> 33;
            value *= 0xc4ceb9fe1a85ec53UL;
            value ^= value >> 33;
            return (value & 0xffffff) / 16777215d;
        }
    }

    public static VoxelColor Color(int band, DeviceCapabilities capabilities, double intensity = 1)
    {
        var color = VisualizerPalette.ForBand(band);
        if (capabilities == DeviceCapabilities.Rgb) return color;
        return VoxelColor.Monochrome((byte)Math.Clamp((int)Math.Round(intensity * 255), 1, 255));
    }
}

internal static class VisualizerGeometry
{
    public static void DrawBoxShell(VoxelScene scene, double radius, VoxelColor color)
    {
        radius = Math.Clamp(radius, 0, 1);
        var halfX = Math.Max(0, (int)Math.Round(radius * (scene.Width - 1) / 2));
        var halfY = Math.Max(0, (int)Math.Round(radius * (scene.Depth - 1) / 2));
        var halfZ = Math.Max(0, (int)Math.Round(radius * (scene.Height - 1) / 2));
        var minX = Math.Max(0, (scene.Width - 1) / 2 - halfX);
        var maxX = Math.Min(scene.Width - 1, scene.Width / 2 + halfX);
        var minY = Math.Max(0, (scene.Depth - 1) / 2 - halfY);
        var maxY = Math.Min(scene.Depth - 1, scene.Depth / 2 + halfY);
        var minZ = Math.Max(0, (scene.Height - 1) / 2 - halfZ);
        var maxZ = Math.Min(scene.Height - 1, scene.Height / 2 + halfZ);
        for (var x = minX; x <= maxX; x++)
        for (var y = minY; y <= maxY; y++)
        for (var z = minZ; z <= maxZ; z++)
            if (x == minX || x == maxX || y == minY || y == maxY || z == minZ || z == maxZ)
                scene[x, y, z] = color;
    }

    public static void DrawSquareRing(VoxelScene scene, double radius, double normalizedZ, VoxelColor color)
    {
        var z = VisualizerMath.Coord(normalizedZ, scene.Height);
        var halfX = Math.Max(0, (int)Math.Round(Math.Clamp(radius, 0, 1) * (scene.Width - 1) / 2));
        var halfY = Math.Max(0, (int)Math.Round(Math.Clamp(radius, 0, 1) * (scene.Depth - 1) / 2));
        var minX = Math.Max(0, (scene.Width - 1) / 2 - halfX);
        var maxX = Math.Min(scene.Width - 1, scene.Width / 2 + halfX);
        var minY = Math.Max(0, (scene.Depth - 1) / 2 - halfY);
        var maxY = Math.Min(scene.Depth - 1, scene.Depth / 2 + halfY);
        for (var x = minX; x <= maxX; x++) { scene[x, minY, z] = color; scene[x, maxY, z] = color; }
        for (var y = minY; y <= maxY; y++) { scene[minX, y, z] = color; scene[maxX, y, z] = color; }
    }

    public static void DrawLine(VoxelScene scene, double x0, double y0, double z0,
        double x1, double y1, double z1, VoxelColor color)
    {
        var steps = Math.Max(2, Math.Max(scene.Width, Math.Max(scene.Depth, scene.Height)) * 2);
        for (var step = 0; step <= steps; step++)
        {
            var t = step / (double)steps;
            scene.SetNormalized(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, z0 + (z1 - z0) * t, color);
        }
    }

    public static void DrawOrbit(VoxelScene scene, int axis, double radius, double phase, int points, VoxelColor color)
    {
        for (var point = 0; point < points; point++)
        {
            var angle = phase + point * Math.Tau / points;
            var a = .5 + Math.Cos(angle) * radius * .5;
            var b = .5 + Math.Sin(angle) * radius * .5;
            if (axis == 0) scene.SetNormalized(.5, a, b, color);
            else if (axis == 1) scene.SetNormalized(a, .5, b, color);
            else scene.SetNormalized(a, b, .5, color);
        }
    }
}
