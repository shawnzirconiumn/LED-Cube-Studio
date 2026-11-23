using LedCube.App.Settings;
using LedCube.App.ViewModels;
using LedCube.App.VisualizerBuilder;
using LedCube.Audio;
using LedCube.Core.Audio;
using LedCube.Core.Devices;
using LedCube.Core.Scene;
using LedCube.Core.Visualization;
using LedCube.Core.Visualization.Builder;
using LedCube.Hardware.Transport;

namespace LedCube.Tests;

public sealed class VisualizerBuilderViewModelTests
{
    [Fact]
    public async Task StartupCreatesACleanVolumeThatRendersInSilence()
    {
        var viewModel = new VisualizerBuilderViewModel(CreateStartupStore());
        await viewModel.InitializeAsync();

        var layer = Assert.Single(viewModel.Layers);
        Assert.Same(layer, viewModel.SelectedLayer);
        Assert.Equal(EffectKind.FullVolume, layer.Effect);
        Assert.Equal(ControlSourceKind.Constant, layer.SourceKind);
        Assert.Equal(ColorMappingKind.Fixed, layer.ColorMapping);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(viewModel.Name, viewModel.DocumentTitle);

        var scene = new VoxelScene(8, 8, 8);
        viewModel.Runtime.Render(scene, AudioAnalysisSnapshot.Silence, Frame());
        Assert.Equal(512, scene.CountLitVoxels());
        Assert.Equal(VoxelColor.FromRgb(0, 90, 128), scene[0, 0, 0]);
    }

    [Fact]
    public async Task FirstStartupEditMarksTheDocumentDirtyAndRequiresDiscardConfirmation()
    {
        var viewModel = new VisualizerBuilderViewModel(CreateStartupStore());
        await viewModel.InitializeAsync();

        viewModel.SelectedLayer!.IntensityPercent = 25;

        Assert.True(viewModel.IsDirty);
        Assert.EndsWith(" *", viewModel.DocumentTitle, StringComparison.Ordinal);
        viewModel.UsePresetCommand.Execute(null);
        Assert.True(viewModel.HasPendingDiscard);
        Assert.Equal(25, viewModel.SelectedLayer.IntensityPercent);
    }

    [Fact]
    public async Task AnExampleLoadsFromTheUntouchedStarterAndLeavesSavedVisualizersIntact()
    {
        var store = CreateStartupStore();
        try
        {
            var savedPath = await store.SaveAsAsync(VisualizerPresets.CreateRain(), "Saved Rain", overwrite: false);
            var savedJson = await File.ReadAllTextAsync(savedPath);
            var viewModel = new VisualizerBuilderViewModel(store);
            await viewModel.InitializeAsync();
            Assert.Equal(savedPath, Assert.Single(viewModel.SavedVisualizers).Path);
            Assert.Equal(EffectKind.FullVolume, viewModel.SelectedLayer!.Effect);

            var example = viewModel.Presets[1];
            viewModel.SelectedPreset = example;
            viewModel.UsePresetCommand.Execute(null);

            Assert.False(viewModel.HasPendingDiscard);
            Assert.Equal(example.Name, viewModel.Name);
            Assert.Equal(EffectKind.SpectrumColumns, viewModel.SelectedLayer!.Effect);
            Assert.True(viewModel.IsDirty);
            Assert.Equal(savedJson, await File.ReadAllTextAsync(savedPath));
            Assert.Single(Directory.EnumerateFiles(store.DirectoryPath));
        }
        finally
        {
            if (Directory.Exists(store.DirectoryPath)) Directory.Delete(store.DirectoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task NewAndEditingAreDirtyAndSuccessfulSaveClearsDirtyState()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"led-cube-builder-{Guid.NewGuid():N}");
        try
        {
            var viewModel = new VisualizerBuilderViewModel(new JsonVisualizerDefinitionStore(directory));

            viewModel.NewCommand.Execute(null);
            Assert.True(viewModel.IsDirty);
            Assert.EndsWith(" *", viewModel.DocumentTitle, StringComparison.Ordinal);

            await viewModel.SaveCurrentAsync();
            Assert.False(viewModel.IsDirty);
            Assert.False(viewModel.DocumentTitle.EndsWith(" *", StringComparison.Ordinal));

            viewModel.SelectedLayer!.Brightness = .45;
            Assert.True(viewModel.IsDirty);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void SwitchingModesPreservesHiddenLayerValues()
    {
        var viewModel = NewBuilder();
        var layer = viewModel.SelectedLayer!;
        layer.Thickness = .37;
        layer.Blend = BlendMode.Additive;
        layer.SourcePhase = .42;

        viewModel.SelectedMode = BuilderMode.Advanced;
        viewModel.SelectedMode = BuilderMode.Expert;
        viewModel.SelectedMode = BuilderMode.Simple;

        var definition = viewModel.GetDefinitionSnapshot().Layers.Single();
        Assert.Equal(.37, definition.Spatial.Thickness, 6);
        Assert.Equal(BlendMode.Additive, definition.Blend);
        Assert.Equal(.42, definition.Source.Phase, 6);
    }

    [Fact]
    public void ContextualControlsOnlyAppearWhenTheRuntimeUsesThem()
    {
        var viewModel = NewBuilder();
        var layer = viewModel.SelectedLayer!;

        layer.Effect = EffectKind.FullVolume;
        layer.SourceKind = ControlSourceKind.Constant;
        layer.ColorMapping = ColorMappingKind.Fixed;
        Assert.False(layer.ShowAxis);
        Assert.False(layer.ShowConstant);
        Assert.False(layer.ShowAudioIntensity);
        Assert.False(layer.ShowSpeed);

        layer.ColorMapping = ColorMappingKind.Gradient;
        Assert.True(layer.ShowAxis);

        layer.SourceKind = ControlSourceKind.AudioBand;
        Assert.True(layer.ShowAudioBand);
        Assert.False(layer.ShowOscillatorFrequency);
        Assert.False(layer.ShowSourcePhase);

        layer.SourceKind = ControlSourceKind.Oscillator;
        Assert.False(layer.ShowAudioBand);
        Assert.True(layer.ShowOscillatorFrequency);
        Assert.True(layer.ShowSourcePhase);

        layer.Effect = EffectKind.Rain;
        Assert.True(layer.ShowSpeed);
        Assert.True(layer.ShowDirection);
        Assert.True(layer.ShowAxis);
        Assert.True(layer.ShowDensity);
        Assert.True(layer.ShowLifetime);
        Assert.True(layer.ShowMotionPhase);
        Assert.False(layer.ShowDecay);

        layer.Effect = EffectKind.Pulse;
        Assert.True(layer.ShowDecay);
        Assert.True(layer.ShowThickness);
        Assert.False(layer.ShowLifetime);
        Assert.False(layer.ShowMotionPhase);
    }

    [Theory]
    [InlineData(EffectKind.FullVolume, "brightness", "brightness")]
    [InlineData(EffectKind.Plane, "brightness", "brightness")]
    [InlineData(EffectKind.SpectrumColumns, "eight frequency bands drive column heights", "column heights")]
    [InlineData(EffectKind.Pulse, "pulse size", "how large the pulse grows")]
    [InlineData(EffectKind.Rain, "how many drops appear", "how many drops")]
    public void ReactToAndReactionStrengthHelpFollowTheSelectedEffect(
        EffectKind effect, string reactToExplanation, string strengthExplanation)
    {
        var layer = NewBuilder().SelectedLayer!;
        layer.Effect = effect == EffectKind.Rain ? EffectKind.FullVolume : EffectKind.Rain;
        layer.SourceKind = ControlSourceKind.OverallLevel;
        var changedProperties = new List<string?>();
        layer.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        layer.Effect = effect;

        Assert.Contains(nameof(LayerEditorViewModel.ReactToHelp), changedProperties);
        Assert.Contains(nameof(LayerEditorViewModel.ReactionStrengthHelp), changedProperties);
        Assert.Contains(reactToExplanation, layer.ReactToHelp, StringComparison.Ordinal);
        Assert.Contains(strengthExplanation, layer.ReactionStrengthHelp, StringComparison.Ordinal);
        Assert.Contains("Music Loudness follows the overall audio level.", layer.ReactToHelp, StringComparison.Ordinal);
    }

    [Fact]
    public void FriendlyUnitsMapToThePersistedNormalizedValues()
    {
        var layer = NewBuilder().SelectedLayer!;

        layer.IntensityPercent = 35;
        layer.ThicknessPercent = 20;
        layer.DensityPercent = 45;
        layer.OffsetPercent = 60;
        layer.FadeSeconds = 1.2;
        layer.SourcePhasePercent = 25;
        layer.MotionPhasePercent = -50;
        layer.FrequencyBandChoice = "Air (6.4–12.8 kHz)";
        layer.ReactionStrengthPercent = 120;
        layer.RainbowOffsetPercent = 30;

        var definition = layer.ToDefinition();
        Assert.Equal(.35, definition.Color.Brightness, 6);
        Assert.Equal(.2, definition.Spatial.Thickness, 6);
        Assert.Equal(.45, definition.Spatial.Density, 6);
        Assert.Equal(.6, definition.Spatial.Offset, 6);
        Assert.Equal(.6, definition.Timing.Decay, 6);
        Assert.Equal(.25, definition.Source.Phase, 6);
        Assert.Equal(-.5, definition.Timing.Phase, 6);
        Assert.Equal(7, definition.Source.Band);
        Assert.Equal(1.2, definition.Source.ReactionStrength, 6);
        Assert.Equal(.3, definition.Color.CycleOffset, 6);
        Assert.Equal("35%", layer.IntensityDisplay);
        Assert.Equal("120%", layer.ReactionStrengthDisplay);
        Assert.Equal("1.2 s", layer.FadeTimeDisplay);
        Assert.Equal("45%", layer.DensityDisplay);
        Assert.Equal("30%", layer.RainbowOffsetDisplay);
    }

    [Fact]
    public void EffectConversionUpdatesOnlyAutomaticLayerNames()
    {
        var automatic = NewBuilder().SelectedLayer!;
        Assert.Equal("Volume 1", automatic.Name);
        automatic.Effect = EffectKind.Rain;
        Assert.Equal("Rain 1", automatic.Name);

        automatic.Name = "Bass Rain";
        automatic.Effect = EffectKind.Pulse;
        Assert.Equal("Bass Rain", automatic.Name);
    }

    [Fact]
    public async Task SaveOverwritesCurrentFileAndSaveAsCreatesAndSelectsANamedCopy()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"led-cube-builder-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonVisualizerDefinitionStore(directory);
            var viewModel = new VisualizerBuilderViewModel(store);
            viewModel.NewCommand.Execute(null);
            Assert.True(await viewModel.SaveAsCurrentAsync("First Show"));
            var firstPath = store.PathForName("First Show");

            viewModel.SelectedLayer!.IntensityPercent = 42;
            await viewModel.SaveCurrentAsync();
            Assert.Equal(.42, (await store.LoadAsync(firstPath)).Layers[0].Color.Brightness, 6);
            Assert.Single(viewModel.SavedVisualizers);

            Assert.True(await viewModel.SaveAsCurrentAsync("Second Show"));
            Assert.Equal(2, viewModel.SavedVisualizers.Count);
            Assert.Contains(viewModel.SavedVisualizers, item => item.Path == store.PathForName("Second Show"));
            Assert.Equal("Second Show", viewModel.Name);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task SaveAsDoesNotOverwriteWithoutConfirmation()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"led-cube-builder-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonVisualizerDefinitionStore(directory);
            var viewModel = new VisualizerBuilderViewModel(store);
            viewModel.NewCommand.Execute(null);
            Assert.True(await viewModel.SaveAsCurrentAsync("Protected"));
            viewModel.SelectedLayer!.IntensityPercent = 25;

            Assert.False(await viewModel.SaveAsCurrentAsync("Protected"));
            Assert.Single(viewModel.SavedVisualizers);
            Assert.NotEqual(.25, (await store.LoadAsync(store.PathForName("Protected"))).Layers[0].Color.Brightness);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void OnePreviewRequestKeepsTheRuntimeUpdatedAfterEdits()
    {
        var viewModel = NewBuilder();
        var requested = 0;
        viewModel.PreviewRequested += runtime =>
        {
            Assert.Same(viewModel.Runtime, runtime);
            requested++;
        };
        viewModel.TryNowCommand.Execute(null);
        Assert.Equal(1, requested);

        var layer = viewModel.SelectedLayer!;
        layer.PrimaryRed = 255;
        layer.PrimaryGreen = 0;
        layer.PrimaryBlue = 0;

        var scene = new VoxelScene(4, 5, 6);
        viewModel.Runtime.Render(scene, AudioAnalysisSnapshot.Silence, Frame());
        Assert.Equal(VoxelColor.Red, scene[2, 3, 4]);
        Assert.Equal(1, requested);
    }

    [Fact]
    public async Task PreviewStatusFollowsTheActiveStreamAcrossDisconnectAndReconnect()
    {
        await using var main = CreatePreviewHost();
        var reportedStates = new List<string>();
        main.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.BuilderPreviewStatus))
                reportedStates.Add(main.BuilderPreviewStatus);
        };

        Assert.Equal("Not Previewing", main.BuilderPreviewStatus);
        main.Builder.NewCommand.Execute(null);
        main.Builder.TryNowCommand.Execute(null);
        Assert.True(main.IsBuilderPreviewSelected);
        Assert.Equal("Preview Ready — Cube Disconnected", reportedStates[^1]);

        await main.ToggleConnectionAsync();
        Assert.Equal("Live on Cube", reportedStates[^1]);
        main.SelectedStreamMode = main.StreamModes[1];
        Assert.Equal("Live on Cube", main.BuilderPreviewStatus);

        await main.ToggleConnectionAsync();
        Assert.Equal("Preview Ready — Cube Disconnected", reportedStates[^1]);
        await main.ToggleConnectionAsync();
        Assert.Equal("Preview Paused — Hardware Test", reportedStates[^1]);
        main.Builder.TryNowCommand.Execute(null);
        Assert.Equal("Preview Paused — Hardware Test", main.BuilderPreviewStatus);
        main.SelectedStreamMode = main.StreamModes[0];
        Assert.Equal("Preview Paused — Hardware Test", main.BuilderPreviewStatus);

        await main.ToggleConnectionAsync();
        Assert.Equal("Preview Ready — Cube Disconnected", reportedStates[^1]);
        await main.ToggleConnectionAsync();
        Assert.Equal("Live on Cube", reportedStates[^1]);
        main.SelectedVisualizer = main.Visualizers[0];
        Assert.Equal("Not Previewing", reportedStates[^1]);
        Assert.False(main.IsBuilderPreviewSelected);
        main.SelectedVisualizer = main.Builder.Runtime;
        Assert.Equal("Live on Cube", reportedStates[^1]);
    }

    [Fact]
    public async Task PreviewStopsReportingLiveWhenTheStreamEndsWithThePortStillOpen()
    {
        var transport = new PreviewTransport();
        await using var main = CreatePreviewHost(transport);
        var streamInactive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        main.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.BuilderPreviewStatus) &&
                main.BuilderPreviewStatus == "Preview Ready — Stream Inactive")
                streamInactive.TrySetResult();
        };
        main.Builder.NewCommand.Execute(null);
        main.Builder.TryNowCommand.Execute(null);
        await main.ToggleConnectionAsync();
        Assert.Equal("Live on Cube", main.BuilderPreviewStatus);

        transport.FailWrites();
        await streamInactive.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(main.IsConnected);
        Assert.True(main.IsBuilderPreviewSelected);
        Assert.Equal("Preview Ready — Stream Inactive", main.BuilderPreviewStatus);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task StopPreviewRestoresThePreviouslySelectedBuiltInVisualizer(int? streamModeIndex)
    {
        await using var main = CreatePreviewHost();
        var builtIn = main.Visualizers[1];
        main.SelectedVisualizer = builtIn;
        if (streamModeIndex is int index)
        {
            main.SelectedStreamMode = main.StreamModes[index];
            await main.ToggleConnectionAsync();
        }

        Assert.Equal("Not Previewing", main.BuilderPreviewStatus);
        main.Builder.NewCommand.Execute(null);
        main.Builder.TryNowCommand.Execute(null);
        Assert.Same(main.Builder.Runtime, main.SelectedVisualizer);
        Assert.True(main.IsBuilderPreviewSelected);

        main.Builder.StopPreviewCommand.Execute(null);

        Assert.Same(builtIn, main.SelectedVisualizer);
        Assert.False(main.IsBuilderPreviewSelected);
        Assert.Equal("Not Previewing", main.BuilderPreviewStatus);
    }

    [Theory]
    [InlineData("Volume")]
    [InlineData("Plane")]
    [InlineData("Spectrum Columns")]
    [InlineData("Pulse")]
    [InlineData("Rain")]
    public void AddingEachEffectCreatesAValidVisibleLayer(string effect)
    {
        var viewModel = new VisualizerBuilderViewModel();
        viewModel.RemoveLayerCommand.Execute(null);
        viewModel.EffectToAdd = effect;
        viewModel.AddLayerCommand.Execute(null);

        var definition = viewModel.GetDefinitionSnapshot();
        Assert.True(VisualizerDefinitionValidator.Validate(definition).IsValid);
        Assert.Single(definition.Layers);
        Assert.Equal(effect, viewModel.SelectedLayer!.EffectChoice);

        var scene = new VoxelScene(8, 8, 8);
        viewModel.Runtime.Render(scene, ActiveAudio(), Frame());
        Assert.True(scene.CountLitVoxels() > 0, effect);
    }

    private static VisualizerBuilderViewModel NewBuilder()
    {
        var viewModel = new VisualizerBuilderViewModel();
        viewModel.NewCommand.Execute(null);
        return viewModel;
    }

    private static JsonVisualizerDefinitionStore CreateStartupStore() =>
        new(Path.Combine(Path.GetTempPath(), $"led-cube-builder-startup-{Guid.NewGuid():N}"));

    private static MainWindowViewModel CreatePreviewHost(PreviewTransport? transport = null) =>
        new(new MemorySettingsStore(), transport ?? new PreviewTransport(), new SilentAudioCaptureFactory())
        {
            SelectedSerialPort = new SerialPortDescriptor("test", "Test port"),
            SelectedAudioSource = new AudioSourceInfo("silent", "Silent test source")
        };

    private static VisualizationFrame Frame() =>
        new(TimeSpan.FromSeconds(.25), TimeSpan.FromMilliseconds(50), 1, DeviceCapabilities.Rgb);

    private static AudioAnalysisSnapshot ActiveAudio()
    {
        double[] bands = [.2, .4, .6, .8, .9, .7, .5, .3];
        return new AudioAnalysisSnapshot(.75, .75, .75, bands, bands, bands, .6, .7, .5, true, 1, DateTimeOffset.UtcNow);
    }

    private sealed class MemorySettingsStore : IApplicationSettingsStore
    {
        public Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApplicationSettings());

        public Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class PreviewTransport : ISerialTransport
    {
        private volatile bool _failWrites;

        public bool IsOpen => PortName is not null;
        public string? PortName { get; private set; }

        public Task OpenAsync(string portName, int baudRate, CancellationToken cancellationToken = default)
        {
            PortName = portName;
            return Task.CompletedTask;
        }

        public Task WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) =>
            _failWrites ? Task.FromException(new IOException("Simulated write failure.")) : Task.CompletedTask;

        public void FailWrites() => _failWrites = true;

        public Task CloseAsync()
        {
            PortName = null;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SilentAudioCaptureFactory : IAudioCaptureFactory
    {
        public Task<IReadOnlyList<AudioSourceInfo>> GetSourcesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AudioSourceInfo>>([]);

        public IAudioCapture Create(AudioSourceInfo source) => new SilentAudioCapture();
    }

    private sealed class SilentAudioCapture : IAudioCapture
    {
        public event EventHandler<AudioBuffer>? DataAvailable { add { } remove { } }
        public event EventHandler<string>? StatusChanged { add { } remove { } }
        public bool IsRunning { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            IsRunning = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => new(StopAsync());
    }
}
