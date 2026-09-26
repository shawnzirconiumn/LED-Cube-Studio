using LedCube.App.Settings;

namespace LedCube.Tests;

public sealed class ApplicationSettingsTests
{
    [Fact]
    public void NumericSettingsAreClampedAndUnsupportedBaudFallsBack()
    {
        var settings = new ApplicationSettings
        {
            BaudRate = -50,
            Sensitivity = 900,
            Decay = -20
        }.Sanitize();

        Assert.Equal(1200, settings.BaudRate);
        Assert.Equal(200, settings.Sensitivity);
        Assert.Equal(0, settings.Decay);
        Assert.Equal(115200, SettingsSelection.ResolveBaudRate([9600, 115200], settings.BaudRate, 115200));
    }

    [Theory]
    [InlineData(460800)]
    [InlineData(2_000_000)]
    [InlineData(38400)]
    public void StaleExperimentalBaudFallsBackForValidatedRgbProfile(int staleBaud)
    {
        var profile = LedCube.Core.Devices.DeviceProfiles.All[0];

        var resolved = SettingsSelection.ResolveBaudRateForProfile(
            profile, [9600, 57600, 115200, 230400, 460800, 2_000_000], staleBaud);

        Assert.Equal(115200, resolved);
    }

    [Fact]
    public async Task StaleExperimentalPresetFieldDoesNotPreventSettingsLoad()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ledcube-settings-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            await File.WriteAllTextAsync(path,
                """{"selectedProfileId":"rgb-8x8x8","baudRate":460800,"transportTestPresetId":"C"}""");

            var settings = await new JsonApplicationSettingsStore(path).LoadAsync();
            var profile = LedCube.Core.Devices.DeviceProfiles.All[0];
            var baud = SettingsSelection.ResolveBaudRateForProfile(
                profile, [115200, 460800, 2_000_000], settings.BaudRate);

            Assert.Equal("rgb-8x8x8", settings.SelectedProfileId);
            Assert.Equal(115200, baud);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void StaleSelectionFallsBackToPreferredAvailableItem()
    {
        var available = new[]
        {
            new Choice("fallback", false),
            new Choice("stable", true)
        };

        var selected = SettingsSelection.ResolveAvailable(
            available, "stale", choice => choice.Id, choice => choice.Preferred);

        Assert.Equal("stable", selected?.Id);
    }

    [Fact]
    public async Task InvalidSettingsFileReturnsSafeDefaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ledcube-settings-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            await File.WriteAllTextAsync(path, "{ invalid json");

            var settings = await new JsonApplicationSettingsStore(path).LoadAsync();

            Assert.Equal(ApplicationSettings.DefaultSensitivity, settings.Sensitivity);
            Assert.Equal(ApplicationSettings.DefaultDecay, settings.Decay);
            Assert.Null(settings.SelectedSerialPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SettingsRoundTripAllPersistedSelections()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ledcube-settings-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var store = new JsonApplicationSettingsStore(path);
            var expected = new ApplicationSettings
            {
                SelectedProfileId = "rgb-8x8x8",
                SelectedSerialPath = "/dev/serial/by-id/cube",
                BaudRate = 115200,
                SelectedAudioSourceId = "output.monitor",
                SelectedVisualizerId = "eight-band-spectrum",
                Sensitivity = 125,
                Decay = 25,
                DiagnosticsVisible = true
            };

            await store.SaveAsync(expected);
            var actual = await store.LoadAsync();

            Assert.Equal(expected, actual);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed record Choice(string Id, bool Preferred);
}
