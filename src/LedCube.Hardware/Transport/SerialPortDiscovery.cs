using System.IO.Ports;

namespace LedCube.Hardware.Transport;

public static class SerialPortDiscovery
{
    public static IReadOnlyList<SerialPortDescriptor> GetPorts()
    {
        if (OperatingSystem.IsLinux())
        {
            var stablePaths = EnumeratePaths("/dev/serial/by-id", "*");
            var fallbackPaths = EnumeratePaths("/dev", "ttyUSB*")
                .Concat(EnumeratePaths("/dev", "ttyACM*"));
            return BuildLinuxPorts(stablePaths, fallbackPaths);
        }

        return SerialPort.GetPortNames().OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new SerialPortDescriptor(name, name))
            .ToArray();
    }

    internal static IReadOnlyList<SerialPortDescriptor> BuildLinuxPorts(
        IEnumerable<string> stablePaths,
        IEnumerable<string> fallbackPaths)
    {
        var ports = new Dictionary<string, SerialPortDescriptor>(StringComparer.Ordinal);
        foreach (var path in stablePaths.OrderBy(path => path, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(path).Replace('_', ' ');
            ports.TryAdd(GetPhysicalIdentity(path), new SerialPortDescriptor(path, $"{name} (stable)", IsStable: true));
        }
        foreach (var path in fallbackPaths.OrderBy(path => path, StringComparer.Ordinal))
            ports.TryAdd(GetPhysicalIdentity(path), new SerialPortDescriptor(path, path));

        return ports.Values.OrderByDescending(port => port.IsStable)
            .ThenBy(port => port.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> EnumeratePaths(string directory, string pattern)
    {
        return Directory.Exists(directory) ? Directory.EnumerateFiles(directory, pattern) : [];
    }

    private static string GetPhysicalIdentity(string path)
    {
        try
        {
            return File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? Path.GetFullPath(path);
        }
        catch (IOException)
        {
            return Path.GetFullPath(path);
        }
        catch (UnauthorizedAccessException)
        {
            return Path.GetFullPath(path);
        }
    }
}
