using LedCube.Hardware.Transport;

namespace LedCube.Tests;

public sealed class SerialPortDiscoveryTests
{
    [Fact]
    public void StableLinuxSymlinkReplacesDuplicateKernelDevicePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ledcube-serial-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var devicePath = Path.Combine(directory, "ttyUSB0");
            var stablePath = Path.Combine(directory, "usb-1a86_USB_Serial-if00-port0");
            File.WriteAllBytes(devicePath, []);
            File.CreateSymbolicLink(stablePath, devicePath);

            var ports = SerialPortDiscovery.BuildLinuxPorts([stablePath], [devicePath]);

            var port = Assert.Single(ports);
            Assert.Equal(stablePath, port.Path);
            Assert.True(port.IsStable);
            Assert.Contains("USB Serial", port.DisplayName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
