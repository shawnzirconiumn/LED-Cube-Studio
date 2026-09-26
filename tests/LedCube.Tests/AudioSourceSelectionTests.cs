using LedCube.Audio;

namespace LedCube.Tests;

public sealed class AudioSourceSelectionTests
{
    private const string SourceList =
        "41\talsa_input.usb-microphone\tmodule-alsa-card.c\ts16le 2ch 48000Hz\tRUNNING\n" +
        "42\talsa_output.pci-0000_01_00.1.hdmi-stereo.monitor\tmodule-alsa-card.c\tfloat32le 2ch 48000Hz\tIDLE\n" +
        "43\talsa_output.pci-0000_09_00.6.analog-stereo.monitor\tmodule-alsa-card.c\tfloat32le 2ch 48000Hz\tRUNNING\n";

    [Fact]
    public void DefaultSinkMonitorIsMarkedAndSelectedWithoutSavedPreference()
    {
        var sources = LinuxPulseDiscovery.ParseSources(
            SourceList, "alsa_output.pci-0000_09_00.6.analog-stereo");

        var selected = AudioSourceSelection.SelectPreferred(sources);

        Assert.NotNull(selected);
        Assert.Equal("alsa_output.pci-0000_09_00.6.analog-stereo.monitor", selected.Id);
        Assert.True(selected.IsSystemDefault);
        Assert.True(selected.IsOutputMonitor);
    }

    [Fact]
    public void AvailableSavedMonitorWinsButStaleSavedSourceFallsBackToCurrentDefault()
    {
        var sources = LinuxPulseDiscovery.ParseSources(
            SourceList, "alsa_output.pci-0000_09_00.6.analog-stereo");

        var saved = AudioSourceSelection.SelectPreferred(
            sources, "alsa_output.pci-0000_01_00.1.hdmi-stereo.monitor");
        var stale = AudioSourceSelection.SelectPreferred(sources, "missing.monitor");

        Assert.Equal("alsa_output.pci-0000_01_00.1.hdmi-stereo.monitor", saved?.Id);
        Assert.Equal("alsa_output.pci-0000_09_00.6.analog-stereo.monitor", stale?.Id);
    }

    [Fact]
    public void NormalFallbackNeverSelectsAnInputSource()
    {
        var sources = new[] { new AudioSourceInfo("microphone", "Microphone") };

        Assert.Null(AudioSourceSelection.SelectPreferred(sources));
    }
}
