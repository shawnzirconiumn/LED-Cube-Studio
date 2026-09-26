using System.Globalization;
using Avalonia.Data.Converters;
using LedCube.Audio;
using LedCube.Hardware.Transport;

namespace LedCube.App.Presentation;

public sealed class FriendlyNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        SerialPortDescriptor port => FriendlyNames.SerialPort(port),
        AudioSourceInfo source => FriendlyNames.AudioSource(source),
        _ => value?.ToString() ?? string.Empty
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public static class FriendlyNames
{
    public static string SerialPort(SerialPortDescriptor? port)
    {
        if (port is null) return "No serial connection";

        var name = port.DisplayName;
        const string stableSuffix = " (stable)";
        if (name.EndsWith(stableSuffix, StringComparison.OrdinalIgnoreCase))
            name = name[..^stableSuffix.Length];

        var interfaceSuffix = name.IndexOf("-if", StringComparison.OrdinalIgnoreCase);
        if (interfaceSuffix >= 0) name = name[..interfaceSuffix];

        if (name.StartsWith("usb-", StringComparison.OrdinalIgnoreCase))
        {
            var descriptionStart = name.IndexOf(' ');
            if (descriptionStart >= 0) name = name[(descriptionStart + 1)..];
        }

        if (name.Contains("USB Serial", StringComparison.OrdinalIgnoreCase))
            name = "USB Serial";
        else if (name.StartsWith("/dev/ttyUSB", StringComparison.OrdinalIgnoreCase) ||
                 name.StartsWith("/dev/ttyACM", StringComparison.OrdinalIgnoreCase))
            name = $"USB Serial ({Path.GetFileName(name)})";

        return port.IsStable ? $"{name} (stable)" : name;
    }

    public static string AudioSource(AudioSourceInfo? source)
    {
        if (source is null) return "No audio source selected";
        if (source.Id == "@DEFAULT_MONITOR@") return "Default System Output";
        if (!source.Id.EndsWith(".monitor", StringComparison.OrdinalIgnoreCase))
            return source.DisplayName;

        var deviceId = source.Id[..^".monitor".Length];
        var profileStart = deviceId.LastIndexOf('.');
        var profile = profileStart >= 0 ? deviceId[(profileStart + 1)..] : deviceId;
        var words = profile.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.ToLowerInvariant() switch
            {
                "hdmi" => "HDMI",
                "usb" => "USB",
                "a2dp" => "A2DP",
                var value => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value)
            });
        return $"{string.Join(' ', words)} Output";
    }
}
