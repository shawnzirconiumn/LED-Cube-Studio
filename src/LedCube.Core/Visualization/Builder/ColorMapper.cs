using LedCube.Core.Devices;
using LedCube.Core.Scene;

namespace LedCube.Core.Visualization.Builder;

public static class ColorMapper
{
    public static VoxelColor Map(ColorMappingDefinition mapping, double position, double sourceValue,
        DeviceCapabilities capabilities, double opacity = 1, bool forceSourceIntensity = false,
        double elapsedSeconds = 0)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        position = Math.Clamp(position, 0, 1);
        sourceValue = Math.Clamp(sourceValue, 0, 1);
        var mix = mapping.Kind == ColorMappingKind.Gradient ? position : 0;
        opacity = Math.Clamp(opacity, 0, 1);
        var intensity = mapping.Brightness * opacity *
                        (mapping.AudioDrivenIntensity || forceSourceIntensity ? sourceValue : 1);
        byte red;
        byte green;
        byte blue;
        if (mapping.Kind == ColorMappingKind.Rainbow)
        {
            var hue = Wrap(position + mapping.CycleOffset + elapsedSeconds * mapping.CycleSpeed);
            (red, green, blue) = Rainbow(hue, intensity);
        }
        else
        {
            red = Channel(mapping.Primary.Red, mapping.Secondary.Red, mix, intensity);
            green = Channel(mapping.Primary.Green, mapping.Secondary.Green, mix, intensity);
            blue = Channel(mapping.Primary.Blue, mapping.Secondary.Blue, mix, intensity);
        }
        var rgb = VoxelColor.FromRgb(red, green, blue);
        return capabilities == DeviceCapabilities.MonochromeIntensity ? VoxelColor.Monochrome(rgb.Intensity) : rgb;
    }

    private static byte Channel(byte first, byte second, double mix, double intensity) =>
        (byte)Math.Clamp((int)Math.Round((first + (second - first) * mix) * intensity), 0, 255);

    private static (byte Red, byte Green, byte Blue) Rainbow(double hue, double intensity)
    {
        var section = hue * 6;
        var index = (int)Math.Floor(section);
        var fraction = section - index;
        var (red, green, blue) = (index % 6) switch
        {
            0 => (1d, fraction, 0d),
            1 => (1d - fraction, 1d, 0d),
            2 => (0d, 1d, fraction),
            3 => (0d, 1d - fraction, 1d),
            4 => (fraction, 0d, 1d),
            _ => (1d, 0d, 1d - fraction)
        };
        return ((byte)Math.Round(red * intensity * 255), (byte)Math.Round(green * intensity * 255),
            (byte)Math.Round(blue * intensity * 255));
    }

    private static double Wrap(double value) => value - Math.Floor(value);
}
