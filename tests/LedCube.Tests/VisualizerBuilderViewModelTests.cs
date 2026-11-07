using LedCube.App.Settings;
using LedCube.App.ViewModels;
using LedCube.App.VisualizerBuilder;
using LedCube.Core.Audio;
using LedCube.Core.Devices;
using LedCube.Core.Scene;
using LedCube.Core.Visualization;
using LedCube.Core.Visualization.Builder;

namespace LedCube.Tests;

public sealed class VisualizerBuilderViewModelTests
{
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
    public void OnePreviewStartMakesSubsequentEditsLive()
    {
        var viewModel = NewBuilder();
        var requested = 0;
        viewModel.PreviewRequested += _ => requested++;
        viewModel.TryNowCommand.Execute(null);
        Assert.True(viewModel.IsPreviewActive);
        Assert.Equal(1, requested);

        var layer = viewModel.SelectedLayer!;
        layer.PrimaryRed = 255;
        layer.PrimaryGreen = 0;
        layer.PrimaryBlue = 0;

        var scene = new VoxelScene(4, 5, 6);
        viewModel.Runtime.Render(scene, AudioAnalysisSnapshot.Silence, Frame());
        Assert.Equal(VoxelColor.Red, scene[2, 3, 4]);
        Assert.Equal(1, requested);
        Assert.True(viewModel.IsPreviewActive);
    }

    [Fact]
    public async Task StopPreviewRestoresThePreviouslySelectedBuiltInVisualizer()
    {
        var main = new MainWindowViewModel(new MemorySettingsStore());
        try
        {
            var builtIn = main.SelectedVisualizer;
            main.Builder.NewCommand.Execute(null);
            main.Builder.TryNowCommand.Execute(null);
            Assert.Same(main.Builder.Runtime, main.SelectedVisualizer);

            main.Builder.StopPreviewCommand.Execute(null);

            Assert.Same(builtIn, main.SelectedVisualizer);
            Assert.False(main.Builder.IsPreviewActive);
        }
        finally
        {
            await main.DisposeAsync();
        }
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
        viewModel.EffectToAdd = effect;
        viewModel.AddLayerCommand.Execute(null);

        var definition = viewModel.GetDefinitionSnapshot();
        Assert.True(VisualizerDefinitionValidator.Validate(definition).IsValid);
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
}
