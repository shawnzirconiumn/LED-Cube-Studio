using LedCube.App.Presentation;
using LedCube.App.ViewModels;
using LedCube.Audio;
using LedCube.Hardware.Transport;

namespace LedCube.Tests;

public sealed class ConnectionPresentationTests
{
    [Fact]
    public void FriendlyNamesDoNotReplaceUnderlyingIdentifiers()
    {
        var port = new SerialPortDescriptor(
            "/dev/serial/by-id/usb-1a86_USB_Serial-if00-port0",
            "usb-1a86 USB Serial-if00-port0 (stable)",
            IsStable: true);
        var source = new AudioSourceInfo(
            "alsa_output.pci-0000_09_00.6.analog-stereo.monitor",
            "alsa_output.pci-0000_09_00.6.analog-stereo.monitor (output monitor, default)",
            IsOutputMonitor: true,
            IsSystemDefault: true);

        Assert.Equal("USB Serial (stable)", FriendlyNames.SerialPort(port));
        Assert.Equal("Analog Stereo Output", FriendlyNames.AudioSource(source));
        Assert.Contains("usb-1a86", port.Path);
        Assert.StartsWith("alsa_output", source.Id);
    }

    [Fact]
    public void SerialSelectionRestoresSavedOrChoosesOnlyUnambiguousPort()
    {
        var fallback = new SerialPortDescriptor("/dev/ttyUSB0", "/dev/ttyUSB0");
        var stable = new SerialPortDescriptor("/dev/serial/by-id/cube", "Cube (stable)", true);
        var otherStable = new SerialPortDescriptor("/dev/serial/by-id/other", "Other (stable)", true);

        Assert.Same(fallback, MainWindowViewModel.SelectPreferredSerialPort([fallback], null));
        Assert.Same(stable, MainWindowViewModel.SelectPreferredSerialPort([fallback, stable], null));
        Assert.Same(otherStable,
            MainWindowViewModel.SelectPreferredSerialPort([fallback, stable, otherStable], otherStable.Path));
        Assert.Null(MainWindowViewModel.SelectPreferredSerialPort([stable, otherStable], null));
    }
}
