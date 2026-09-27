using LedCube.Core.Devices;
using LedCube.Core.Scene;

namespace LedCube.Core.Visualization.Builder;

public static class ColorMapper
{
    public static VoxelColor Map(ColorMappingDefinition mapping, double position, double sourceValue,
        DeviceCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        position = Math.Clamp(position, 0, 1);
        sourceValue = Math.Clamp(sourceValue, 0, 1);
        var mix = mapping.Kind == ColorMappingKind.Gradient ? position : 0;
        var intensity = mapping.Brightness * (mapping.AudioDrivenIntensity ? sourceValue : 1);
        var red = Channel(mapping.Primary.Red, mapping.Secondary.Red, mix, intensity);
        var green = Channel(mapping.Primary.Green, mapping.Secondary.Green, mix, intensity);
        var blue = Channel(mapping.Primary.Blue, mapping.Secondary.Blue, mix, intensity);
        var rgb = VoxelColor.FromRgb(red, green, blue);
        return capabilities == DeviceCapabilities.MonochromeIntensity ? VoxelColor.Monochrome(rgb.Intensity) : rgb;
    }

    private static byte Channel(byte first, byte second, double mix, double intensity) =>
        (byte)Math.Clamp((int)Math.Round((first + (second - first) * mix) * intensity), 0, 255);
}
