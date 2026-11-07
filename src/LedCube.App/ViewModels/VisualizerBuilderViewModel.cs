using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using LedCube.App.VisualizerBuilder;
using LedCube.Core.Visualization.Builder;

namespace LedCube.App.ViewModels;

public enum BuilderMode { Simple, Advanced, Expert }
public sealed record SaveAsDialogRequest(string SuggestedName, bool ConfirmOverwrite);

public sealed class VisualizerBuilderViewModel : INotifyPropertyChanged
{
    private enum PendingDocumentAction { None, New, Open, Preset }

    private readonly JsonVisualizerDefinitionStore _store;
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = "My Visualizer";
    private string _description = string.Empty;
    private string? _currentPath;
    private LayerEditorViewModel? _selectedLayer;
    private SavedVisualizerInfo? _selectedSavedVisualizer;
    private SavedVisualizerInfo? _pendingOpen;
    private VisualizerDefinition? _selectedPreset;
    private VisualizerDefinition? _pendingPreset;
    private BuilderMode _selectedMode;
    private EffectKind _effectToAdd = EffectKind.FullVolume;
    private string _status = "Choose an example or add a layer to get started.";
    private string _validationMessage = string.Empty;
    private bool _isPreviewActive;
    private bool _isDirty;
    private PendingDocumentAction _pendingAction;

    public VisualizerBuilderViewModel(JsonVisualizerDefinitionStore? store = null)
    {
        _store = store ?? new JsonVisualizerDefinitionStore();
        Runtime = new CustomVisualizerRuntime(BuildDefinition());
        Presets = VisualizerPresets.CreateAll();
        SelectedPreset = Presets[0];
        NewCommand = new RelayCommand(RequestNew);
        AddLayerCommand = new RelayCommand(AddLayer);
        RemoveLayerCommand = new RelayCommand(RemoveLayer);
        MoveLayerUpCommand = new RelayCommand(() => MoveLayer(-1));
        MoveLayerDownCommand = new RelayCommand(() => MoveLayer(1));
        UsePresetCommand = new RelayCommand(RequestUsePreset);
        TryNowCommand = new RelayCommand(StartPreview);
        StopPreviewCommand = new RelayCommand(StopPreview);
        SaveCommand = new AsyncCommand(SaveCurrentAsync);
        SaveAsCommand = new AsyncCommand(RequestSaveAsAsync);
        OpenCommand = new AsyncCommand(RequestOpenAsync);
        ContinueDiscardCommand = new AsyncCommand(ContinueDiscardAsync);
        CancelDiscardCommand = new RelayCommand(ClearPendingAction);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<CustomVisualizerRuntime>? PreviewRequested;
    public event Action? PreviewStopped;
    public event Func<SaveAsDialogRequest, Task<string?>>? SaveAsRequested;

    public CustomVisualizerRuntime Runtime { get; }
    public ObservableCollection<LayerEditorViewModel> Layers { get; } = [];
    public ObservableCollection<SavedVisualizerInfo> SavedVisualizers { get; } = [];
    public IReadOnlyList<VisualizerDefinition> Presets { get; }
    public IReadOnlyList<BuilderMode> Modes { get; } = Enum.GetValues<BuilderMode>();
    public IReadOnlyList<string> EffectChoices => BuilderLabels.EffectChoices;
    public IReadOnlyList<string> SourceChoices => BuilderLabels.SourceChoices;
    public IReadOnlyList<string> ColorMappingChoices => BuilderLabels.ColorMappingChoices;
    public IReadOnlyList<string> BlendChoices => BuilderLabels.BlendChoices;
    public IReadOnlyList<string> AxisChoices => BuilderLabels.AxisChoices;
    public IReadOnlyList<string> DirectionChoices => BuilderLabels.DirectionChoices;
    public IReadOnlyList<string> FrequencyBandChoices => BuilderLabels.FrequencyBandChoices;
    public RelayCommand NewCommand { get; }
    public RelayCommand AddLayerCommand { get; }
    public RelayCommand RemoveLayerCommand { get; }
    public RelayCommand MoveLayerUpCommand { get; }
    public RelayCommand MoveLayerDownCommand { get; }
    public RelayCommand UsePresetCommand { get; }
    public RelayCommand TryNowCommand { get; }
    public RelayCommand StopPreviewCommand { get; }
    public RelayCommand CancelDiscardCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand SaveAsCommand { get; }
    public AsyncCommand OpenCommand { get; }
    public AsyncCommand ContinueDiscardCommand { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (!Set(ref _name, value)) return;
            OnPropertyChanged(nameof(DocumentTitle));
            DefinitionChanged();
        }
    }
    public string Description { get => _description; set { if (Set(ref _description, value)) DefinitionChanged(); } }
    public string DocumentTitle => $"{(string.IsNullOrWhiteSpace(Name) ? "Untitled Visualizer" : Name)}{(IsDirty ? " *" : string.Empty)}";
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string ValidationMessage { get => _validationMessage; private set { if (Set(ref _validationMessage, value)) OnPropertyChanged(nameof(HasValidationMessage)); } }
    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);
    public bool IsDirty { get => _isDirty; private set { if (Set(ref _isDirty, value)) OnPropertyChanged(nameof(DocumentTitle)); } }
    public bool IsPreviewActive { get => _isPreviewActive; private set { if (Set(ref _isPreviewActive, value)) OnPropertyChanged(nameof(PreviewStateText)); } }
    public string PreviewStateText => IsPreviewActive ? "● LIVE ON CUBE" : "○ NOT LIVE";
    public bool HasPendingDiscard => _pendingAction != PendingDocumentAction.None;
    public string DiscardPrompt => "You have unsaved changes. Discard them and continue?";
    public LayerEditorViewModel? SelectedLayer { get => _selectedLayer; set => Set(ref _selectedLayer, value); }
    public SavedVisualizerInfo? SelectedSavedVisualizer { get => _selectedSavedVisualizer; set => Set(ref _selectedSavedVisualizer, value); }
    public VisualizerDefinition? SelectedPreset { get => _selectedPreset; set => Set(ref _selectedPreset, value); }

    public string EffectToAdd
    {
        get => BuilderLabels.Effect(_effectToAdd);
        set
        {
            var parsed = BuilderLabels.ParseEffect(value);
            if (_effectToAdd == parsed) return;
            _effectToAdd = parsed;
            OnPropertyChanged();
        }
    }

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
        if (Layers.Count == 0) UsePresetCore(SelectedPreset ?? Presets[0]);
    }

    public void SetPreviewActiveFromHost(bool active)
    {
        IsPreviewActive = active;
        if (!active && Status.StartsWith("Live preview", StringComparison.Ordinal))
            Status = "Preview stopped. The normal visualizer is active.";
    }

    public VisualizerDefinition GetDefinitionSnapshot() => BuildDefinition();

    public Task SaveCurrentAsync() => _currentPath is null ? RequestSaveAsAsync() : SaveAsync();

    public async Task<bool> SaveAsCurrentAsync(string name, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (!UpdateRuntime()) return false;
        var oldName = Name;
        Name = name.Trim();
        try
        {
            _currentPath = await _store.SaveAsAsync(BuildDefinition(), Name, overwrite).ConfigureAwait(true);
            await RefreshSavedAsync().ConfigureAwait(true);
            IsDirty = false;
            Status = $"Saved {Path.GetFileName(_currentPath)}.";
            return true;
        }
        catch (VisualizerFileExistsException)
        {
            Name = oldName;
            Status = "A saved visualizer with that name already exists. Confirm overwrite or choose another name.";
            return false;
        }
        catch (Exception)
        {
            Name = oldName;
            Status = "Could not save this visualizer. Check the storage location and try again.";
            return false;
        }
    }

    private void RequestNew()
    {
        if (IsDirty) { SetPendingAction(PendingDocumentAction.New); return; }
        NewCore();
    }

    private void NewCore()
    {
        _id = Guid.NewGuid().ToString("N");
        _currentPath = null;
        _name = "My Visualizer";
        _description = string.Empty;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
        Layers.Clear();
        AddLayer();
        IsDirty = true;
        Status = "New visualizer ready. Choose an effect, then preview it.";
    }

    private void AddLayer()
    {
        var definition = CreateLayer(_effectToAdd, Layers.Count + 1);
        var editor = new LayerEditorViewModel(definition, DefinitionChanged);
        Layers.Add(editor);
        SelectedLayer = editor;
        DefinitionChanged();
        Status = $"Added {BuilderLabels.Effect(_effectToAdd)}. It is ready to preview.";
    }

    private static LayerDefinition CreateLayer(EffectKind effect, int number)
    {
        var layer = new LayerDefinition
        {
            Name = $"{BuilderLabels.Effect(effect)} {number}", Effect = effect,
            Source = new ControlSourceDefinition { Kind = ControlSourceKind.Constant, Constant = 1 },
            Color = new ColorMappingDefinition
            {
                Primary = new RgbColorDefinition(0, 180, 255), Secondary = new RgbColorDefinition(255, 0, 180),
                AudioDrivenIntensity = false
            }
        };
        switch (effect)
        {
            case EffectKind.Plane:
                layer.Spatial.Thickness = .2; layer.Timing.Speed = .25; break;
            case EffectKind.SpectrumColumns:
                layer.Color.Kind = ColorMappingKind.Gradient;
                layer.Color.Primary = new RgbColorDefinition(0, 255, 80);
                layer.Color.Secondary = new RgbColorDefinition(255, 40, 0); break;
            case EffectKind.Pulse:
                layer.Source.Kind = ControlSourceKind.Oscillator; layer.Source.Frequency = .5;
                layer.Spatial.Thickness = .2; layer.Timing.Decay = .35;
                layer.Color.Kind = ColorMappingKind.Gradient; break;
            case EffectKind.Rain:
                layer.Spatial.Direction = -1; layer.Spatial.Density = .35;
                layer.Timing.Speed = .75; layer.Timing.Lifetime = .35;
                layer.Color.Kind = ColorMappingKind.Gradient; break;
        }
        return layer;
    }

    private void RemoveLayer()
    {
        if (SelectedLayer is null) return;
        var index = Layers.IndexOf(SelectedLayer);
        Layers.Remove(SelectedLayer);
        SelectedLayer = Layers.Count == 0 ? null : Layers[Math.Clamp(index, 0, Layers.Count - 1)];
        DefinitionChanged();
    }

    private void MoveLayer(int offset)
    {
        if (SelectedLayer is null) return;
        var oldIndex = Layers.IndexOf(SelectedLayer); var newIndex = oldIndex + offset;
        if (newIndex < 0 || newIndex >= Layers.Count) return;
        Layers.Move(oldIndex, newIndex); DefinitionChanged();
    }

    private void RequestUsePreset()
    {
        if (SelectedPreset is null) return;
        if (IsDirty) { _pendingPreset = SelectedPreset; SetPendingAction(PendingDocumentAction.Preset); return; }
        UsePresetCore(SelectedPreset);
    }

    private void UsePresetCore(VisualizerDefinition preset)
    {
        var copy = VisualizerDefinitionSerializer.Deserialize(VisualizerDefinitionSerializer.Serialize(preset));
        copy.Id = Guid.NewGuid().ToString("N");
        Load(copy, null, true);
        Status = $"Loaded example: {copy.Name}. Customize it, preview it, then save.";
    }

    private void StartPreview()
    {
        if (!UpdateRuntime()) return;
        IsPreviewActive = true;
        PreviewRequested?.Invoke(Runtime);
        Status = "Live preview active. Further edits update the cube automatically.";
    }

    private void StopPreview()
    {
        if (!IsPreviewActive) return;
        IsPreviewActive = false;
        PreviewStopped?.Invoke();
        Status = "Preview stopped. The previous visualizer is active.";
    }

    private async Task RequestSaveAsAsync()
    {
        var handler = SaveAsRequested;
        var name = handler is null ? Name : await handler(new SaveAsDialogRequest(Name, false)).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(name)) return;
        if (await SaveAsCurrentAsync(name).ConfigureAwait(true)) return;
        if (!File.Exists(_store.PathForName(name))) return;
        var confirmed = handler is null ? null : await handler(new SaveAsDialogRequest(name, true)).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(confirmed))
            await SaveAsCurrentAsync(confirmed, true).ConfigureAwait(true);
    }

    private async Task SaveAsync()
    {
        if (!UpdateRuntime() || _currentPath is null) return;
        try
        {
            _currentPath = await _store.SaveAsync(BuildDefinition(), _currentPath).ConfigureAwait(true);
            await RefreshSavedAsync().ConfigureAwait(true);
            IsDirty = false;
            Status = $"Saved {Path.GetFileName(_currentPath)}.";
        }
        catch (Exception) { Status = "Could not save this visualizer. Check the storage location and try again."; }
    }

    private Task RequestOpenAsync()
    {
        if (SelectedSavedVisualizer is null) { Status = "Choose a saved visualizer to open."; return Task.CompletedTask; }
        if (IsDirty) { _pendingOpen = SelectedSavedVisualizer; SetPendingAction(PendingDocumentAction.Open); return Task.CompletedTask; }
        return OpenCoreAsync(SelectedSavedVisualizer);
    }

    private async Task OpenCoreAsync(SavedVisualizerInfo saved)
    {
        try
        {
            var definition = await _store.LoadAsync(saved.Path).ConfigureAwait(true);
            Load(definition, saved.Path, false);
            Status = $"Opened {definition.Name}.";
        }
        catch (Exception) { Status = "Could not open this visualizer. The file may be invalid or unsupported."; }
    }

    private async Task ContinueDiscardAsync()
    {
        var action = _pendingAction; var saved = _pendingOpen; var preset = _pendingPreset;
        ClearPendingAction();
        switch (action)
        {
            case PendingDocumentAction.New: NewCore(); break;
            case PendingDocumentAction.Open when saved is not null: await OpenCoreAsync(saved).ConfigureAwait(true); break;
            case PendingDocumentAction.Preset when preset is not null: UsePresetCore(preset); break;
        }
    }

    private void SetPendingAction(PendingDocumentAction action)
    {
        _pendingAction = action;
        OnPropertyChanged(nameof(HasPendingDiscard));
        Status = "Unsaved changes have not been discarded.";
    }

    private void ClearPendingAction()
    {
        _pendingAction = PendingDocumentAction.None; _pendingOpen = null; _pendingPreset = null;
        OnPropertyChanged(nameof(HasPendingDiscard));
    }

    private async Task RefreshSavedAsync()
    {
        var saved = await _store.ListAsync().ConfigureAwait(true);
        SavedVisualizers.Clear();
        foreach (var item in saved) SavedVisualizers.Add(item);
        SelectedSavedVisualizer = SavedVisualizers.FirstOrDefault(item => item.Path == _currentPath)
                                  ?? SavedVisualizers.FirstOrDefault();
    }

    private void Load(VisualizerDefinition definition, string? path, bool dirty)
    {
        _id = definition.Id; _currentPath = path; _name = definition.Name; _description = definition.Description;
        OnPropertyChanged(nameof(Name)); OnPropertyChanged(nameof(Description));
        Layers.Clear();
        foreach (var layer in definition.Layers) Layers.Add(new LayerEditorViewModel(layer, DefinitionChanged));
        SelectedLayer = Layers.FirstOrDefault();
        UpdateRuntime();
        IsDirty = dirty;
        OnPropertyChanged(nameof(DocumentTitle));
    }

    private VisualizerDefinition BuildDefinition() => new()
    {
        SchemaVersion = VisualizerDefinition.CurrentSchemaVersion, Id = _id, Name = Name, Description = Description,
        Layers = Layers.Select(layer => layer.ToDefinition()).ToList()
    };

    private void DefinitionChanged()
    {
        IsDirty = true;
        UpdateRuntime();
        if (IsPreviewActive && !HasValidationMessage) Status = "Live preview updated.";
    }

    private bool UpdateRuntime()
    {
        var definition = BuildDefinition();
        var validation = VisualizerDefinitionValidator.Validate(definition);
        if (!validation.IsValid) { ValidationMessage = FriendlyValidation(validation.Errors[0]); return false; }
        if (!Runtime.TryUpdate(definition))
        {
            ValidationMessage = "This visualizer is temporarily incomplete. Fix the highlighted values to preview it.";
            return false;
        }
        ValidationMessage = string.Empty;
        return true;
    }

    private static string FriendlyValidation(string error)
    {
        if (error.Contains("name is required", StringComparison.OrdinalIgnoreCase)) return "Enter a visualizer name.";
        if (error.Contains("needs a name", StringComparison.OrdinalIgnoreCase)) return "Give the selected layer a name.";
        if (error.Contains("band", StringComparison.OrdinalIgnoreCase)) return "Choose one of the eight frequency bands.";
        if (error.Contains("lifetime", StringComparison.OrdinalIgnoreCase)) return "Lifetime must be greater than zero.";
        if (error.Contains("thickness", StringComparison.OrdinalIgnoreCase)) return "Thickness must be greater than zero.";
        if (error.Contains("brightness", StringComparison.OrdinalIgnoreCase)) return "Intensity must be between 0% and 100%.";
        return "Check the selected layer's values.";
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(propertyName); return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class LayerEditorViewModel : INotifyPropertyChanged
{
    private readonly Action _changed;
    private readonly LayerDefinition _definition;
    private string _colorValidationMessage = string.Empty;
    private bool _nameIsAutomatic;

    public LayerEditorViewModel(LayerDefinition definition, Action changed)
    {
        (_definition, _changed) = (definition, changed);
        _nameIsAutomatic = IsAutomaticName(definition.Name, definition.Effect);
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _definition.Name;
        set
        {
            if (_definition.Name == value) return;
            _definition.Name = value;
            _nameIsAutomatic = false;
            OnPropertyChanged();
            _changed();
        }
    }
    public bool Enabled { get => _definition.Enabled; set => Set(_definition.Enabled, value, v => _definition.Enabled = v); }
    public EffectKind Effect
    {
        get => _definition.Effect;
        set
        {
            if (_definition.Effect == value) return;
            var suffix = _nameIsAutomatic ? AutomaticNameSuffix(_definition.Name) : string.Empty;
            _definition.Effect = value;
            if (_nameIsAutomatic)
            {
                _definition.Name = BuilderLabels.Effect(value) + suffix;
                OnPropertyChanged(nameof(Name));
            }
            OnPropertyChanged();
            NotifyPresentation();
            _changed();
        }
    }
    public ControlSourceKind SourceKind { get => _definition.Source.Kind; set { Set(_definition.Source.Kind, value, v => _definition.Source.Kind = v); NotifyPresentation(); } }
    public double Constant { get => _definition.Source.Constant; set => Set(_definition.Source.Constant, value, v => _definition.Source.Constant = v); }
    public int Band
    {
        get => _definition.Source.Band;
        set
        {
            var clamped = Math.Clamp(value, 0, BuilderLabels.FrequencyBandChoices.Count - 1);
            Set(_definition.Source.Band, clamped, v => _definition.Source.Band = v);
            OnPropertyChanged(nameof(FrequencyBandChoice));
            OnPropertyChanged(nameof(Summary));
        }
    }
    public double Frequency { get => _definition.Source.Frequency; set { Set(_definition.Source.Frequency, value, v => _definition.Source.Frequency = v); OnPropertyChanged(nameof(AutoPulseDisplay)); } }
    public double ReactionStrength { get => _definition.Source.ReactionStrength; set { Set(_definition.Source.ReactionStrength, value, v => _definition.Source.ReactionStrength = v); OnPropertyChanged(nameof(ReactionStrengthPercent)); OnPropertyChanged(nameof(ReactionStrengthDisplay)); } }
    public double SourcePhase { get => _definition.Source.Phase; set => Set(_definition.Source.Phase, value, v => _definition.Source.Phase = v); }
    public Axis Axis { get => _definition.Spatial.Axis; set => Set(_definition.Spatial.Axis, value, v => _definition.Spatial.Axis = v); }
    public int Direction { get => _definition.Spatial.Direction; set => Set(_definition.Spatial.Direction, value, v => _definition.Spatial.Direction = v); }
    public double Offset { get => _definition.Spatial.Offset; set { Set(_definition.Spatial.Offset, value, v => _definition.Spatial.Offset = v); OnPropertyChanged(nameof(OffsetPercent)); OnPropertyChanged(nameof(OffsetDisplay)); } }
    public double Thickness { get => _definition.Spatial.Thickness; set { Set(_definition.Spatial.Thickness, value, v => _definition.Spatial.Thickness = v); OnPropertyChanged(nameof(ThicknessPercent)); OnPropertyChanged(nameof(ThicknessDisplay)); } }
    public double Density { get => _definition.Spatial.Density; set { Set(_definition.Spatial.Density, value, v => _definition.Spatial.Density = v); OnPropertyChanged(nameof(DensityPercent)); OnPropertyChanged(nameof(DensityDisplay)); } }
    public double Speed { get => _definition.Timing.Speed; set { Set(_definition.Timing.Speed, value, v => _definition.Timing.Speed = v); OnPropertyChanged(nameof(SpeedDisplay)); } }
    public double Phase { get => _definition.Timing.Phase; set => Set(_definition.Timing.Phase, value, v => _definition.Timing.Phase = v); }
    public double Decay { get => _definition.Timing.Decay; set { Set(_definition.Timing.Decay, value, v => _definition.Timing.Decay = v); OnPropertyChanged(nameof(FadeSeconds)); OnPropertyChanged(nameof(FadeTimeDisplay)); } }
    public double Lifetime { get => _definition.Timing.Lifetime; set { Set(_definition.Timing.Lifetime, value, v => _definition.Timing.Lifetime = v); OnPropertyChanged(nameof(LifetimeDisplay)); } }
    public ColorMappingKind ColorMapping { get => _definition.Color.Kind; set { Set(_definition.Color.Kind, value, v => _definition.Color.Kind = v); NotifyPresentation(); } }
    public bool AudioDrivenIntensity { get => _definition.Color.AudioDrivenIntensity; set => Set(_definition.Color.AudioDrivenIntensity, value, v => _definition.Color.AudioDrivenIntensity = v); }
    public double Brightness { get => _definition.Color.Brightness; set { Set(_definition.Color.Brightness, value, v => _definition.Color.Brightness = v); OnPropertyChanged(nameof(IntensityPercent)); OnPropertyChanged(nameof(IntensityDisplay)); } }
    public double RainbowOffset { get => _definition.Color.CycleOffset; set { Set(_definition.Color.CycleOffset, value, v => _definition.Color.CycleOffset = v); OnPropertyChanged(nameof(RainbowOffsetPercent)); OnPropertyChanged(nameof(RainbowOffsetDisplay)); } }
    public double RainbowSpeed { get => _definition.Color.CycleSpeed; set { Set(_definition.Color.CycleSpeed, value, v => _definition.Color.CycleSpeed = v); OnPropertyChanged(nameof(RainbowSpeedDisplay)); } }
    public BlendMode Blend { get => _definition.Blend; set { Set(_definition.Blend, value, v => _definition.Blend = v); OnPropertyChanged(nameof(BlendHelp)); } }

    public string EffectChoice { get => BuilderLabels.Effect(Effect); set => Effect = BuilderLabels.ParseEffect(value); }
    public string SourceChoice { get => BuilderLabels.Source(SourceKind); set => SourceKind = BuilderLabels.ParseSource(value); }
    public string ColorMappingChoice { get => BuilderLabels.ColorMapping(ColorMapping); set => ColorMapping = BuilderLabels.ParseColorMapping(value); }
    public string BlendChoice { get => BuilderLabels.Blend(Blend); set => Blend = BuilderLabels.ParseBlend(value); }
    public string AxisChoice { get => BuilderLabels.Axis(Axis); set => Axis = BuilderLabels.ParseAxis(value); }
    public string DirectionChoice { get => BuilderLabels.Direction(Direction); set => Direction = BuilderLabels.ParseDirection(value); }
    public string FrequencyBandChoice { get => BuilderLabels.FrequencyBand(Band); set => Band = BuilderLabels.ParseFrequencyBand(value); }
    public string Summary => SourceKind == ControlSourceKind.AudioBand
        ? $"{BuilderLabels.Effect(Effect)} — Frequency Band ({BuilderLabels.FrequencyBandShort(Band)})"
        : $"{BuilderLabels.Effect(Effect)} — {BuilderLabels.Source(SourceKind)}";
    public string EffectHelp => Effect switch
    {
        EffectKind.FullVolume => "Fills the entire cube with one color or a directional gradient.",
        EffectKind.Plane => "Draws a flat sheet that can travel through the cube.",
        EffectKind.SpectrumColumns => "Draws the eight analyzed frequency bands as columns.",
        EffectKind.Pulse => "Draws an expanding shell whose size follows the selected input.",
        _ => "Draws falling points with an optional fading trail."
    };
    public string ReactToLabel => "React To";
    public string AxisLabel => Effect switch
    {
        EffectKind.FullVolume => "Gradient Direction",
        EffectKind.SpectrumColumns => "Growth Direction",
        _ => "Travel Axis"
    };
    public string SpeedLabel => Effect == EffectKind.Rain ? "Fall Speed" : "Travel Speed";
    public string SpeedHelp => "Cube lengths traveled per second.";
    public string ReactToHelp => SourceKind switch
    {
        ControlSourceKind.OverallLevel => "Follows the current music loudness and energy.",
        ControlSourceKind.AudioBand => "Follows one selected frequency range from the music.",
        ControlSourceKind.BeatTrigger => "Responds to detected beats and their fading envelope.",
        ControlSourceKind.Oscillator => "Repeats automatically at the selected rate, even in silence.",
        _ => "Keeps the layer at a steady input level."
    };
    public string BlendHelp => Blend switch
    {
        BlendMode.Additive => "Adds this layer's light to layers below it.",
        BlendMode.Overwrite => "Replaces lit voxels from layers below it.",
        _ => "Keeps the brightest value from overlapping layers."
    };

    public bool ShowAudioBand => SourceKind == ControlSourceKind.AudioBand;
    public bool ShowOscillatorFrequency => SourceKind == ControlSourceKind.Oscillator;
    public bool ShowConstant => false;
    public bool ShowReactionStrength => SourceKind is ControlSourceKind.OverallLevel or ControlSourceKind.AudioBand or ControlSourceKind.BeatTrigger;
    public bool ShowSpeed => Effect is EffectKind.Plane or EffectKind.Rain;
    public bool ShowDirection => Effect is EffectKind.Plane or EffectKind.SpectrumColumns or EffectKind.Rain;
    public bool ShowAxis => Effect is EffectKind.Plane or EffectKind.SpectrumColumns or EffectKind.Rain ||
                            Effect == EffectKind.FullVolume && ColorMapping != ColorMappingKind.Fixed;
    public bool ShowOffset => Effect == EffectKind.Plane;
    public bool ShowThickness => Effect is EffectKind.Plane or EffectKind.Pulse;
    public bool ShowDensity => Effect == EffectKind.Rain;
    public bool ShowDecay => Effect == EffectKind.Pulse;
    public bool ShowLifetime => Effect == EffectKind.Rain;
    public bool ShowPrimaryColor => ColorMapping != ColorMappingKind.Rainbow;
    public bool ShowSecondaryColor => ColorMapping == ColorMappingKind.Gradient;
    public bool ShowRainbowControls => ColorMapping == ColorMappingKind.Rainbow;
    public bool ShowAudioIntensity => false;
    public bool ShowSourcePhase => SourceKind == ControlSourceKind.Oscillator;
    public bool ShowMotionPhase => Effect == EffectKind.Rain;
    public bool ShowExpertParameters => ShowSourcePhase || ShowMotionPhase;

    public double IntensityPercent { get => Brightness * 100; set => Brightness = Math.Clamp(value, 0, 100) / 100; }
    public double ReactionStrengthPercent { get => ReactionStrength * 100; set => ReactionStrength = Math.Clamp(value, 25, 300) / 100; }
    public double ConstantPercent { get => Constant * 100; set => Constant = Math.Clamp(value, 0, 100) / 100; }
    public double OffsetPercent { get => Offset * 100; set => Offset = Math.Clamp(value, 0, 100) / 100; }
    public double ThicknessPercent { get => Thickness * 100; set => Thickness = Math.Clamp(value, .1, 100) / 100; }
    public double DensityPercent { get => Density * 100; set => Density = Math.Clamp(value, 0, 100) / 100; }
    public double FadeSeconds { get => Decay * 2; set => Decay = Math.Clamp(value, 0, 2) / 2; }
    public double SourcePhasePercent { get => SourcePhase * 100; set => SourcePhase = value / 100; }
    public double MotionPhasePercent { get => Phase * 100; set => Phase = value / 100; }
    public double RainbowOffsetPercent { get => RainbowOffset * 100; set => RainbowOffset = Math.Clamp(value, 0, 100) / 100; }

    public string IntensityDisplay => $"{IntensityPercent:0}%";
    public string ReactionStrengthDisplay => $"{ReactionStrengthPercent:0}%";
    public string SpeedDisplay => $"{Speed:0.0#} cube/s";
    public string FadeTimeDisplay => $"{FadeSeconds:0.0} s";
    public string LifetimeDisplay => $"{Lifetime:0.0} s";
    public string DensityDisplay => $"{DensityPercent:0}%";
    public string OffsetDisplay => $"{OffsetPercent:0}%";
    public string ThicknessDisplay => $"{ThicknessPercent:0}%";
    public string AutoPulseDisplay => $"{Frequency:0.0} Hz";
    public string RainbowOffsetDisplay => $"{RainbowOffsetPercent:0}%";
    public string RainbowSpeedDisplay => $"{RainbowSpeed:0.0#} cycle/s";

    public string PrimaryColor { get => Format(_definition.Color.Primary); set => SetColor(value, true); }
    public string SecondaryColor { get => Format(_definition.Color.Secondary); set => SetColor(value, false); }
    public Avalonia.Media.Color PrimaryPickerColor
    {
        get => Avalonia.Media.Color.FromRgb(_definition.Color.Primary.Red, _definition.Color.Primary.Green, _definition.Color.Primary.Blue);
        set => SetPickerColor(value, true);
    }
    public Avalonia.Media.Color SecondaryPickerColor
    {
        get => Avalonia.Media.Color.FromRgb(_definition.Color.Secondary.Red, _definition.Color.Secondary.Green, _definition.Color.Secondary.Blue);
        set => SetPickerColor(value, false);
    }
    public int PrimaryRed { get => _definition.Color.Primary.Red; set => SetChannel(true, 0, value); }
    public int PrimaryGreen { get => _definition.Color.Primary.Green; set => SetChannel(true, 1, value); }
    public int PrimaryBlue { get => _definition.Color.Primary.Blue; set => SetChannel(true, 2, value); }
    public int SecondaryRed { get => _definition.Color.Secondary.Red; set => SetChannel(false, 0, value); }
    public int SecondaryGreen { get => _definition.Color.Secondary.Green; set => SetChannel(false, 1, value); }
    public int SecondaryBlue { get => _definition.Color.Secondary.Blue; set => SetChannel(false, 2, value); }
    public string ColorValidationMessage { get => _colorValidationMessage; private set { if (SetField(ref _colorValidationMessage, value)) OnPropertyChanged(nameof(HasColorValidationMessage)); } }
    public bool HasColorValidationMessage => !string.IsNullOrEmpty(ColorValidationMessage);

    public LayerDefinition ToDefinition() => VisualizerDefinitionSerializer.Deserialize(VisualizerDefinitionSerializer.Serialize(
        new VisualizerDefinition { Layers = [_definition] })).Layers[0];

    private void SetColor(string value, bool primary)
    {
        if (!TryParse(value, out var color))
        {
            ColorValidationMessage = "Use a six-digit hex color, such as #00B4FF.";
            return;
        }
        if (primary) _definition.Color.Primary = color; else _definition.Color.Secondary = color;
        ColorValidationMessage = string.Empty; NotifyColor(primary); _changed();
    }

    private void SetChannel(bool primary, int channel, int value)
    {
        var old = primary ? _definition.Color.Primary : _definition.Color.Secondary;
        var clamped = (byte)Math.Clamp(value, 0, 255);
        var next = channel switch
        {
            0 => new RgbColorDefinition(clamped, old.Green, old.Blue),
            1 => new RgbColorDefinition(old.Red, clamped, old.Blue),
            _ => new RgbColorDefinition(old.Red, old.Green, clamped)
        };
        if (next == old) return;
        if (primary) _definition.Color.Primary = next; else _definition.Color.Secondary = next;
        ColorValidationMessage = string.Empty; NotifyColor(primary); _changed();
    }

    private void NotifyColor(bool primary)
    {
        OnPropertyChanged(primary ? nameof(PrimaryColor) : nameof(SecondaryColor));
        OnPropertyChanged(primary ? nameof(PrimaryPickerColor) : nameof(SecondaryPickerColor));
        OnPropertyChanged(primary ? nameof(PrimaryRed) : nameof(SecondaryRed));
        OnPropertyChanged(primary ? nameof(PrimaryGreen) : nameof(SecondaryGreen));
        OnPropertyChanged(primary ? nameof(PrimaryBlue) : nameof(SecondaryBlue));
    }

    private void SetPickerColor(Avalonia.Media.Color value, bool primary)
    {
        var next = new RgbColorDefinition(value.R, value.G, value.B);
        var old = primary ? _definition.Color.Primary : _definition.Color.Secondary;
        if (next == old) return;
        if (primary) _definition.Color.Primary = next; else _definition.Color.Secondary = next;
        ColorValidationMessage = string.Empty;
        NotifyColor(primary);
        _changed();
    }

    private void NotifyPresentation()
    {
        foreach (var property in new[]
        {
            nameof(EffectChoice), nameof(SourceChoice), nameof(ColorMappingChoice), nameof(Summary), nameof(EffectHelp), nameof(ReactToLabel),
            nameof(ReactToHelp), nameof(AxisLabel), nameof(SpeedLabel), nameof(SpeedHelp),
            nameof(ShowAudioBand), nameof(ShowOscillatorFrequency), nameof(ShowConstant), nameof(ShowReactionStrength), nameof(ShowSpeed),
            nameof(ShowDirection), nameof(ShowAxis), nameof(ShowOffset), nameof(ShowThickness), nameof(ShowDensity), nameof(ShowDecay),
            nameof(ShowLifetime), nameof(ShowPrimaryColor), nameof(ShowSecondaryColor), nameof(ShowRainbowControls), nameof(ShowAudioIntensity), nameof(ShowSourcePhase),
            nameof(ShowMotionPhase), nameof(ShowExpertParameters)
        }) OnPropertyChanged(property);
    }

    private void Set<T>(T oldValue, T value, Action<T> assign, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(oldValue, value)) return;
        assign(value); OnPropertyChanged(name); _changed();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(name); return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static string Format(RgbColorDefinition color) => $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
    private static bool IsAutomaticName(string name, EffectKind effect) =>
        name == BuilderLabels.Effect(effect) || name.StartsWith(BuilderLabels.Effect(effect) + " ", StringComparison.Ordinal) &&
        int.TryParse(name[(BuilderLabels.Effect(effect).Length + 1)..], out _);
    private static string AutomaticNameSuffix(string name)
    {
        var index = name.LastIndexOf(' ');
        return index >= 0 && int.TryParse(name[(index + 1)..], out _) ? name[index..] : string.Empty;
    }
    private static bool TryParse(string value, out RgbColorDefinition color)
    {
        color = new RgbColorDefinition();
        var text = value.Trim().TrimStart('#');
        if (text.Length != 6 || !int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)) return false;
        color = new RgbColorDefinition((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb); return true;
    }
}

internal static class BuilderLabels
{
    public static IReadOnlyList<string> EffectChoices { get; } = ["Volume", "Plane", "Spectrum Columns", "Pulse", "Rain"];
    public static IReadOnlyList<string> SourceChoices { get; } = ["Constant", "Music Loudness", "Frequency Band", "Beat", "Automatic Pulse"];
    public static IReadOnlyList<string> ColorMappingChoices { get; } = ["Solid", "Gradient", "Rainbow"];
    public static IReadOnlyList<string> BlendChoices { get; } = ["Brightest Wins", "Add Light", "Overwrite"];
    public static IReadOnlyList<string> AxisChoices { get; } = ["Left ↔ Right (X)", "Front ↔ Back (Y)", "Bottom ↔ Top (Z)"];
    public static IReadOnlyList<string> DirectionChoices { get; } = ["Forward / Up", "Reverse / Down"];
    public static IReadOnlyList<string> FrequencyBandChoices { get; } =
    [
        "Sub Bass (50–100 Hz)", "Bass (100–200 Hz)", "Low Mids (200–400 Hz)", "Mids (400–800 Hz)",
        "Upper Mids (800 Hz–1.6 kHz)", "Presence (1.6–3.2 kHz)", "Brilliance (3.2–6.4 kHz)", "Air (6.4–12.8 kHz)"
    ];

    public static string Effect(EffectKind value) => value switch
    { EffectKind.FullVolume => "Volume", EffectKind.SpectrumColumns => "Spectrum Columns", _ => value.ToString() };
    public static EffectKind ParseEffect(string value) => value switch
    { "Volume" => EffectKind.FullVolume, "Spectrum Columns" => EffectKind.SpectrumColumns, _ => Enum.Parse<EffectKind>(value) };
    public static string Source(ControlSourceKind value) => value switch
    { ControlSourceKind.OverallLevel => "Music Loudness", ControlSourceKind.AudioBand => "Frequency Band",
        ControlSourceKind.BeatTrigger => "Beat", ControlSourceKind.Oscillator => "Automatic Pulse", _ => value.ToString() };
    public static ControlSourceKind ParseSource(string value) => value switch
    { "Music Loudness" => ControlSourceKind.OverallLevel, "Frequency Band" => ControlSourceKind.AudioBand,
        "Beat" => ControlSourceKind.BeatTrigger, "Automatic Pulse" => ControlSourceKind.Oscillator,
        _ => Enum.Parse<ControlSourceKind>(value) };
    public static string ColorMapping(ColorMappingKind value) => value switch
    { ColorMappingKind.Fixed => "Solid", ColorMappingKind.Gradient => "Gradient", _ => "Rainbow" };
    public static ColorMappingKind ParseColorMapping(string value) => value switch
    { "Solid" or "Single Color" => ColorMappingKind.Fixed, "Rainbow" => ColorMappingKind.Rainbow, _ => ColorMappingKind.Gradient };
    public static string Blend(BlendMode value) => value switch
    { BlendMode.Max => "Brightest Wins", BlendMode.Additive => "Add Light", _ => "Overwrite" };
    public static BlendMode ParseBlend(string value) => value switch
    { "Brightest Wins" => BlendMode.Max, "Add Light" => BlendMode.Additive, _ => BlendMode.Overwrite };
    public static string Axis(Axis value) => AxisChoices[(int)value];
    public static Axis ParseAxis(string value) => value.StartsWith("Left", StringComparison.Ordinal) ? LedCube.Core.Visualization.Builder.Axis.X
        : value.StartsWith("Front", StringComparison.Ordinal) ? LedCube.Core.Visualization.Builder.Axis.Y : LedCube.Core.Visualization.Builder.Axis.Z;
    public static string Direction(int value) => value < 0 ? "Reverse / Down" : "Forward / Up";
    public static int ParseDirection(string value) => value.StartsWith("Reverse", StringComparison.Ordinal) ? -1 : 1;
    public static string FrequencyBand(int value) => FrequencyBandChoices[Math.Clamp(value, 0, FrequencyBandChoices.Count - 1)];
    public static int ParseFrequencyBand(string value)
    {
        for (var index = 0; index < FrequencyBandChoices.Count; index++)
            if (FrequencyBandChoices[index] == value) return index;
        return 0;
    }
    public static string FrequencyBandShort(int value)
    {
        var label = FrequencyBand(Math.Clamp(value, 0, FrequencyBandChoices.Count - 1));
        var range = label.IndexOf(" (", StringComparison.Ordinal);
        return range < 0 ? label : label[..range];
    }
}
