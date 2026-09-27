using LedCube.Core.Audio;
using LedCube.Core.Devices;
using LedCube.Core.Scene;

namespace LedCube.Core.Visualization.Builder;

public sealed class CustomVisualizerRuntime : IVisualizer
{
    private CompiledDefinition _compiled;
    private string _id;
    private string _displayName;
    private string _description;

    public CustomVisualizerRuntime(VisualizerDefinition definition)
    {
        _compiled = Compile(definition);
        _id = $"custom:{definition.Id}";
        _displayName = definition.Name;
        _description = definition.Description;
    }

    public string Id => Volatile.Read(ref _id);
    public string DisplayName => Volatile.Read(ref _displayName);
    public string Description => Volatile.Read(ref _description);
    public VisualizerCategory Category => VisualizerCategory.Volume;
    public DeviceCapabilities? RequiredCapability => null;
    public string? LastUpdateError { get; private set; }

    public bool TryUpdate(VisualizerDefinition definition)
    {
        try
        {
            var next = Compile(definition);
            Volatile.Write(ref _compiled, next);
            Volatile.Write(ref _id, $"custom:{definition.Id}");
            Volatile.Write(ref _displayName, definition.Name);
            Volatile.Write(ref _description, definition.Description);
            LastUpdateError = null;
            return true;
        }
        catch (VisualizerDefinitionException exception)
        {
            LastUpdateError = exception.Message;
            return false;
        }
    }

    public void Render(VoxelScene scene, AudioAnalysisSnapshot audio, VisualizationFrame frame) =>
        Volatile.Read(ref _compiled).Render(scene, audio, frame);

    private static CompiledDefinition Compile(VisualizerDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        VisualizerDefinitionValidator.ThrowIfInvalid(definition);
        // Runtime layers own an immutable snapshot so an incomplete editor mutation cannot
        // leak into the last successfully compiled output.
        var snapshot = VisualizerDefinitionSerializer.Deserialize(VisualizerDefinitionSerializer.Serialize(definition));
        return new CompiledDefinition(snapshot.Layers.Where(layer => layer.Enabled).Select(CompiledLayer.Create).ToArray());
    }

    private sealed class CompiledDefinition(CompiledLayer[] layers)
    {
        private VoxelScene? _scratch;

        public void Render(VoxelScene scene, AudioAnalysisSnapshot audio, VisualizationFrame frame)
        {
            scene.Clear();
            if (_scratch is null || _scratch.Width != scene.Width || _scratch.Depth != scene.Depth || _scratch.Height != scene.Height)
                _scratch = new VoxelScene(scene.Width, scene.Depth, scene.Height);
            foreach (var layer in layers)
            {
                _scratch.Clear();
                layer.Render(_scratch, audio, frame);
                Composite(scene, _scratch, layer.Blend);
            }
        }

        private static void Composite(VoxelScene destination, VoxelScene source, BlendMode mode)
        {
            for (var z = 0; z < destination.Height; z++)
            for (var y = 0; y < destination.Depth; y++)
            for (var x = 0; x < destination.Width; x++)
            {
                var over = source[x, y, z];
                if (!over.IsLit) continue;
                var under = destination[x, y, z];
                destination[x, y, z] = mode switch
                {
                    BlendMode.Overwrite => over,
                    BlendMode.Max => new VoxelColor(Math.Max(under.R, over.R), Math.Max(under.G, over.G),
                        Math.Max(under.B, over.B), Math.Max(under.Intensity, over.Intensity)),
                    BlendMode.Additive => new VoxelColor(Add(under.R, over.R), Add(under.G, over.G),
                        Add(under.B, over.B), Add(under.Intensity, over.Intensity)),
                    _ => over
                };
            }
        }

        private static byte Add(byte left, byte right) => (byte)Math.Min(255, left + right);
    }

    private abstract class CompiledLayer(LayerDefinition definition)
    {
        protected readonly LayerDefinition Definition = definition;
        public BlendMode Blend => Definition.Blend;
        public abstract void Render(VoxelScene scene, AudioAnalysisSnapshot audio, VisualizationFrame frame);
        protected double Source(AudioAnalysisSnapshot audio, VisualizationFrame frame) =>
            ControlSourceEvaluator.Evaluate(Definition.Source, audio, frame.Elapsed);
        protected VoxelColor Color(double position, double source, DeviceCapabilities capabilities) =>
            ColorMapper.Map(Definition.Color, position, source, capabilities);
        public static CompiledLayer Create(LayerDefinition definition) => definition.Effect switch
        {
            EffectKind.FullVolume => new FullVolumeLayer(definition),
            EffectKind.Plane => new PlaneLayer(definition),
            EffectKind.SpectrumColumns => new SpectrumColumnsLayer(definition),
            EffectKind.Pulse => new PulseLayer(definition),
            EffectKind.Rain => new RainLayer(definition),
            _ => throw new VisualizerDefinitionException($"Unsupported effect '{definition.Effect}'.")
        };
        protected static double Coordinate(int x, int y, int z, VoxelScene scene, Axis axis) => axis switch
        { Axis.X => Unit(x, scene.Width), Axis.Y => Unit(y, scene.Depth), _ => Unit(z, scene.Height) };
        protected static double Unit(int value, int size) => size <= 1 ? 0 : value / (double)(size - 1);
        protected static double Wrap(double value) => value - Math.Floor(value);
    }

    private sealed class FullVolumeLayer(LayerDefinition definition) : CompiledLayer(definition)
    {
        public override void Render(VoxelScene scene, AudioAnalysisSnapshot audio, VisualizationFrame frame)
        {
            var source = Source(audio, frame);
            for (var z = 0; z < scene.Height; z++) for (var y = 0; y < scene.Depth; y++) for (var x = 0; x < scene.Width; x++)
                scene[x, y, z] = Color(Coordinate(x, y, z, scene, Definition.Spatial.Axis), source, frame.Capabilities);
        }
    }

    private sealed class PlaneLayer(LayerDefinition definition) : CompiledLayer(definition)
    {
        public override void Render(VoxelScene scene, AudioAnalysisSnapshot audio, VisualizationFrame frame)
        {
            var source = Source(audio, frame);
            var position = Wrap(Definition.Spatial.Offset + Definition.Timing.Phase +
                Definition.Spatial.Direction * Definition.Timing.Speed * frame.Elapsed.TotalSeconds);
            for (var z = 0; z < scene.Height; z++) for (var y = 0; y < scene.Depth; y++) for (var x = 0; x < scene.Width; x++)
            {
                var coordinate = Coordinate(x, y, z, scene, Definition.Spatial.Axis);
                var distance = Math.Abs(coordinate - position);
                distance = Math.Min(distance, 1 - distance);
                if (distance <= Definition.Spatial.Thickness / 2)
                    scene[x, y, z] = Color(coordinate, source, frame.Capabilities);
            }
        }
    }

    private sealed class SpectrumColumnsLayer(LayerDefinition definition) : CompiledLayer(definition)
    {
        public override void Render(VoxelScene scene, AudioAnalysisSnapshot audio, VisualizationFrame frame)
        {
            var modulation = Source(audio, frame);
            for (var z = 0; z < scene.Height; z++) for (var y = 0; y < scene.Depth; y++) for (var x = 0; x < scene.Width; x++)
            {
                var column = Definition.Spatial.Axis == Axis.X ? y : x;
                var columnCount = Definition.Spatial.Axis == Axis.X ? scene.Depth : scene.Width;
                var band = Math.Clamp((int)((long)column * Math.Max(1, audio.Spectrum.Count) / Math.Max(1, columnCount)), 0,
                    Math.Max(0, audio.Spectrum.Count - 1));
                var level = audio.Spectrum.Count == 0 ? 0 : Math.Clamp(audio.Spectrum[band] * modulation, 0, 1);
                var height = Coordinate(x, y, z, scene, Definition.Spatial.Axis);
                if ((Definition.Spatial.Direction > 0 && height <= level) || (Definition.Spatial.Direction < 0 && height >= 1 - level))
                    scene[x, y, z] = Color(height, Math.Max(level, .001), frame.Capabilities);
            }
        }
    }

    private sealed class PulseLayer(LayerDefinition definition) : CompiledLayer(definition)
    {
        public override void Render(VoxelScene scene, AudioAnalysisSnapshot audio, VisualizationFrame frame)
        {
            var source = Source(audio, frame);
            var radius = source * .87;
            for (var z = 0; z < scene.Height; z++) for (var y = 0; y < scene.Depth; y++) for (var x = 0; x < scene.Width; x++)
            {
                var dx = Unit(x, scene.Width) - .5; var dy = Unit(y, scene.Depth) - .5; var dz = Unit(z, scene.Height) - .5;
                var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (Math.Abs(distance - radius) <= Definition.Spatial.Thickness / 2)
                    scene[x, y, z] = Color(Math.Clamp(distance / .87, 0, 1), source, frame.Capabilities);
            }
        }
    }

    private sealed class RainLayer(LayerDefinition definition) : CompiledLayer(definition)
    {
        public override void Render(VoxelScene scene, AudioAnalysisSnapshot audio, VisualizationFrame frame)
        {
            var source = Source(audio, frame);
            var count = Math.Max(1, (int)Math.Ceiling(scene.Width * scene.Depth * Definition.Spatial.Density));
            var axisSize = Definition.Spatial.Axis switch { Axis.X => scene.Width, Axis.Y => scene.Depth, _ => scene.Height };
            for (var index = 0; index < count; index++)
            {
                var a = Noise(index * 3 + 11); var b = Noise(index * 3 + 29);
                var travel = Wrap(Noise(index * 3 + 47) + Definition.Timing.Phase + Definition.Spatial.Direction * frame.Elapsed.TotalSeconds *
                    Definition.Timing.Speed / Math.Max(.001, Definition.Timing.Lifetime));
                var axisCoordinate = VoxelScene.ScaleCoordinate(travel, axisSize);
                var first = VoxelScene.ScaleCoordinate(a, Definition.Spatial.Axis == Axis.X ? scene.Depth : scene.Width);
                var second = VoxelScene.ScaleCoordinate(b, Definition.Spatial.Axis == Axis.Z ? scene.Depth : scene.Height);
                var color = Color(travel, source, frame.Capabilities);
                switch (Definition.Spatial.Axis)
                {
                    case Axis.X: scene[axisCoordinate, first, second] = color; break;
                    case Axis.Y: scene[first, axisCoordinate, second] = color; break;
                    default: scene[first, second, axisCoordinate] = color; break;
                }
            }
        }

        private static double Noise(int seed)
        {
            var value = unchecked((uint)seed * 747796405u + 2891336453u);
            value = ((value >> ((int)(value >> 28) + 4)) ^ value) * 277803737u;
            value = (value >> 22) ^ value;
            return value / (double)uint.MaxValue;
        }
    }
}
