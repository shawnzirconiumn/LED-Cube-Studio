using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using LedCube.App.VisualizerBuilder;
using LedCube.Core.Visualization.Builder;

namespace LedCube.App.ViewModels;

public enum BuilderMode { Simple, Advanced, Expert }

public sealed class VisualizerBuilderViewModel : INotifyPropertyChanged
{
    private readonly JsonVisualizerDefinitionStore _store;
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = "My Visualizer";
    private string _description = string.Empty;
    private string? _currentPath;
    private LayerEditorViewModel? _selectedLayer;
    private SavedVisualizerInfo? _selectedSavedVisualizer;
    private VisualizerDefinition? _selectedPreset;
    private BuilderMode _selectedMode;
    private string _status = "Start with an example or add a layer.";
    private bool _previewActive;

    public VisualizerBuilderViewModel(JsonVisualizerDefinitionStore? store = null)
    {
        _store = store ?? new JsonVisualizerDefinitionStore();
        Runtime = new CustomVisualizerRuntime(BuildDefinition());
        Presets = VisualizerPresets.CreateAll();
        SelectedPreset = Presets[0];
        NewCommand = new RelayCommand(New);
        AddLayerCommand = new RelayCommand(AddLayer);
        RemoveLayerCommand = new RelayCommand(RemoveLayer);
        MoveLayerUpCommand = new RelayCommand(() => MoveLayer(-1));
        MoveLayerDownCommand = new RelayCommand(() => MoveLayer(1));
        UsePresetCommand = new RelayCommand(UsePreset);
        TryNowCommand = new RelayCommand(TryNow);
        SaveCommand = new AsyncCommand(() => SaveAsync(false));
        SaveAsCommand = new AsyncCommand(() => SaveAsync(true));
        OpenCommand = new AsyncCommand(OpenAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<CustomVisualizerRuntime>? PreviewRequested;

    public CustomVisualizerRuntime Runtime { get; }
    public ObservableCollection<LayerEditorViewModel> Layers { get; } = [];
    public ObservableCollection<SavedVisualizerInfo> SavedVisualizers { get; } = [];
    public IReadOnlyList<VisualizerDefinition> Presets { get; }
    public IReadOnlyList<BuilderMode> Modes { get; } = Enum.GetValues<BuilderMode>();
    public IReadOnlyList<EffectKind> Effects { get; } = Enum.GetValues<EffectKind>();
    public IReadOnlyList<ControlSourceKind> Sources { get; } = Enum.GetValues<ControlSourceKind>();
    public IReadOnlyList<ColorMappingKind> ColorMappings { get; } = Enum.GetValues<ColorMappingKind>();
    public IReadOnlyList<BlendMode> BlendModes { get; } = Enum.GetValues<BlendMode>();
    public IReadOnlyList<Axis> Axes { get; } = Enum.GetValues<Axis>();
    public IReadOnlyList<int> Directions { get; } = [-1, 1];
    public RelayCommand NewCommand { get; }
    public RelayCommand AddLayerCommand { get; }
    public RelayCommand RemoveLayerCommand { get; }
    public RelayCommand MoveLayerUpCommand { get; }
    public RelayCommand MoveLayerDownCommand { get; }
    public RelayCommand UsePresetCommand { get; }
    public RelayCommand TryNowCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand SaveAsCommand { get; }
    public AsyncCommand OpenCommand { get; }

    public string Name { get => _name; set { if (Set(ref _name, value)) DefinitionChanged(); } }
    public string Description { get => _description; set { if (Set(ref _description, value)) DefinitionChanged(); } }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public LayerEditorViewModel? SelectedLayer { get => _selectedLayer; set => Set(ref _selectedLayer, value); }
    public SavedVisualizerInfo? SelectedSavedVisualizer { get => _selectedSavedVisualizer; set => Set(ref _selectedSavedVisualizer, value); }
    public VisualizerDefinition? SelectedPreset { get => _selectedPreset; set => Set(ref _selectedPreset, value); }
    public BuilderMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (!Set(ref _selectedMode, value)) return;
            OnPropertyChanged(nameof(ShowAdvancedControls));
            OnPropertyChanged(nameof(ShowExpertControls));
        }
    }
    public bool ShowAdvancedControls => SelectedMode is BuilderMode.Advanced or BuilderMode.Expert;
    public bool ShowExpertControls => SelectedMode == BuilderMode.Expert;
    public string StorageLocation => _store.DirectoryPath;

    public async Task InitializeAsync()
    {
        await RefreshSavedAsync().ConfigureAwait(false);
        if (Layers.Count == 0) UsePreset();
    }

    private void New()
    {
        _id = Guid.NewGuid().ToString("N"); _currentPath = null; Name = "My Visualizer"; Description = string.Empty;
        Layers.Clear(); AddLayer(); Status = "New visualizer ready.";
    }

    private void AddLayer()
    {
        var editor = new LayerEditorViewModel(new LayerDefinition { Name = $"Layer {Layers.Count + 1}" }, DefinitionChanged);
        Layers.Add(editor); SelectedLayer = editor; DefinitionChanged();
    }

    private void RemoveLayer()
    {
        if (SelectedLayer is null) return;
        var index = Layers.IndexOf(SelectedLayer); Layers.Remove(SelectedLayer);
        SelectedLayer = Layers.Count == 0 ? null : Layers[Math.Clamp(index, 0, Layers.Count - 1)]; DefinitionChanged();
    }

    private void MoveLayer(int offset)
    {
        if (SelectedLayer is null) return;
        var oldIndex = Layers.IndexOf(SelectedLayer); var newIndex = oldIndex + offset;
        if (newIndex < 0 || newIndex >= Layers.Count) return;
        Layers.Move(oldIndex, newIndex); DefinitionChanged();
    }

    private void UsePreset()
    {
        if (SelectedPreset is null) return;
        var copy = VisualizerDefinitionSerializer.Deserialize(VisualizerDefinitionSerializer.Serialize(SelectedPreset));
        copy.Id = Guid.NewGuid().ToString("N"); Load(copy, null); Status = $"Loaded example: {copy.Name}.";
    }

    private void TryNow()
    {
        DefinitionChanged();
        if (Runtime.LastUpdateError is not null) return;
        _previewActive = true; PreviewRequested?.Invoke(Runtime); Status = "Live preview active; edits update the connected cube immediately.";
    }

    private async Task SaveAsync(bool saveAs)
    {
        try
        {
            var definition = BuildDefinition();
            _currentPath = await _store.SaveAsync(definition, saveAs ? null : _currentPath).ConfigureAwait(true);
            await RefreshSavedAsync().ConfigureAwait(true); Status = $"Saved {Path.GetFileName(_currentPath)}.";
        }
        catch (Exception exception) { Status = exception.Message; }
    }

    private async Task OpenAsync()
    {
        if (SelectedSavedVisualizer is null) { Status = "Choose a saved visualizer first."; return; }
        try
        {
            var definition = await _store.LoadAsync(SelectedSavedVisualizer.Path).ConfigureAwait(true);
            Load(definition, SelectedSavedVisualizer.Path); Status = $"Opened {definition.Name}.";
        }
        catch (Exception exception) { Status = exception.Message; }
    }

    private async Task RefreshSavedAsync()
    {
        var saved = await _store.ListAsync().ConfigureAwait(true);
        SavedVisualizers.Clear(); foreach (var item in saved) SavedVisualizers.Add(item);
        SelectedSavedVisualizer = SavedVisualizers.FirstOrDefault(item => item.Path == _currentPath) ?? SavedVisualizers.FirstOrDefault();
    }

    private void Load(VisualizerDefinition definition, string? path)
    {
        _id = definition.Id; _currentPath = path; _name = definition.Name; _description = definition.Description;
        OnPropertyChanged(nameof(Name)); OnPropertyChanged(nameof(Description));
        Layers.Clear(); foreach (var layer in definition.Layers) Layers.Add(new LayerEditorViewModel(layer, DefinitionChanged));
        SelectedLayer = Layers.FirstOrDefault(); DefinitionChanged();
    }

    private VisualizerDefinition BuildDefinition() => new()
    {
        SchemaVersion = VisualizerDefinition.CurrentSchemaVersion, Id = _id, Name = Name, Description = Description,
        Layers = Layers.Select(layer => layer.ToDefinition()).ToList()
    };

    private void DefinitionChanged()
    {
        var definition = BuildDefinition();
        if (!Runtime.TryUpdate(definition)) Status = Runtime.LastUpdateError ?? "Definition is temporarily invalid.";
        else if (_previewActive) Status = "Live preview updated.";
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(propertyName); return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class LayerEditorViewModel : INotifyPropertyChanged
{
    private readonly Action _changed;
    private LayerDefinition _definition;
    public LayerEditorViewModel(LayerDefinition definition, Action changed) => (_definition, _changed) = (definition, changed);
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get => _definition.Name; set => Set(_definition.Name, value, v => _definition.Name = v); }
    public bool Enabled { get => _definition.Enabled; set => Set(_definition.Enabled, value, v => _definition.Enabled = v); }
    public EffectKind Effect { get => _definition.Effect; set => Set(_definition.Effect, value, v => _definition.Effect = v); }
    public ControlSourceKind SourceKind { get => _definition.Source.Kind; set => Set(_definition.Source.Kind, value, v => _definition.Source.Kind = v); }
    public double Constant { get => _definition.Source.Constant; set => Set(_definition.Source.Constant, value, v => _definition.Source.Constant = v); }
    public int Band { get => _definition.Source.Band; set => Set(_definition.Source.Band, value, v => _definition.Source.Band = v); }
    public double Frequency { get => _definition.Source.Frequency; set => Set(_definition.Source.Frequency, value, v => _definition.Source.Frequency = v); }
    public Axis Axis { get => _definition.Spatial.Axis; set => Set(_definition.Spatial.Axis, value, v => _definition.Spatial.Axis = v); }
    public int Direction { get => _definition.Spatial.Direction; set => Set(_definition.Spatial.Direction, value, v => _definition.Spatial.Direction = v); }
    public double Offset { get => _definition.Spatial.Offset; set => Set(_definition.Spatial.Offset, value, v => _definition.Spatial.Offset = v); }
    public double Thickness { get => _definition.Spatial.Thickness; set => Set(_definition.Spatial.Thickness, value, v => _definition.Spatial.Thickness = v); }
    public double Density { get => _definition.Spatial.Density; set => Set(_definition.Spatial.Density, value, v => _definition.Spatial.Density = v); }
    public double Speed { get => _definition.Timing.Speed; set => Set(_definition.Timing.Speed, value, v => _definition.Timing.Speed = v); }
    public double Phase { get => _definition.Timing.Phase; set => Set(_definition.Timing.Phase, value, v => _definition.Timing.Phase = v); }
    public double Decay { get => _definition.Timing.Decay; set => Set(_definition.Timing.Decay, value, v => _definition.Timing.Decay = v); }
    public double Lifetime { get => _definition.Timing.Lifetime; set => Set(_definition.Timing.Lifetime, value, v => _definition.Timing.Lifetime = v); }
    public ColorMappingKind ColorMapping { get => _definition.Color.Kind; set => Set(_definition.Color.Kind, value, v => _definition.Color.Kind = v); }
    public bool AudioDrivenIntensity { get => _definition.Color.AudioDrivenIntensity; set => Set(_definition.Color.AudioDrivenIntensity, value, v => _definition.Color.AudioDrivenIntensity = v); }
    public double Brightness { get => _definition.Color.Brightness; set => Set(_definition.Color.Brightness, value, v => _definition.Color.Brightness = v); }
    public BlendMode Blend { get => _definition.Blend; set => Set(_definition.Blend, value, v => _definition.Blend = v); }
    public string PrimaryColor { get => Format(_definition.Color.Primary); set => SetColor(value, true); }
    public string SecondaryColor { get => Format(_definition.Color.Secondary); set => SetColor(value, false); }

    public LayerDefinition ToDefinition() => VisualizerDefinitionSerializer.Deserialize(VisualizerDefinitionSerializer.Serialize(
        new VisualizerDefinition { Layers = [_definition] })).Layers[0];

    private void SetColor(string value, bool primary)
    {
        if (!TryParse(value, out var color)) return;
        if (primary) _definition.Color.Primary = color; else _definition.Color.Secondary = color;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(primary ? nameof(PrimaryColor) : nameof(SecondaryColor))); _changed();
    }
    private void Set<T>(T oldValue, T value, Action<T> assign, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(oldValue, value)) return;
        assign(value); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); _changed();
    }
    private static string Format(RgbColorDefinition color) => $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
    private static bool TryParse(string value, out RgbColorDefinition color)
    {
        color = new RgbColorDefinition(); var text = value.Trim().TrimStart('#');
        if (text.Length != 6 || !int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)) return false;
        color = new RgbColorDefinition((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb); return true;
    }
}
