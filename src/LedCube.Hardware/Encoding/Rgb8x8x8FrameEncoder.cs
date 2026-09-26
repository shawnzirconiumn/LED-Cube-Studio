using LedCube.Core.Devices;
using LedCube.Core.Scene;

namespace LedCube.Hardware.Encoding;

/// <summary>
/// Encodes the independently observed two-plane protocol used by the 8x8x8 RGB cube.
/// RGB values are quantized to the nearest of 0, 128, and 255, with upward ties:
/// 0-63 activates neither plane, 64-191 activates plane 0, and 192-255 activates both.
/// </summary>
public sealed class Rgb8x8x8FrameEncoder : IFrameEncoder
{
    private const int PacketLength = 197;
    private const int PayloadOffset = 5;
    private const int MediumThreshold = 64;
    private const int HighThreshold = 192;

    public string Id => DeviceProfiles.Rgb8x8x8EncoderId;

    public EncodedFrame Encode(VoxelScene scene, CubeDeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(profile);

        if (scene.Width != 8 || scene.Depth != 8 || scene.Height != 8)
            throw new ArgumentException("The RGB 8x8x8 encoder requires an 8x8x8 scene.", nameof(scene));
        if (profile.Width != 8 || profile.Depth != 8 || profile.Height != 8)
            throw new ArgumentException("The RGB 8x8x8 encoder requires an 8x8x8 device profile.", nameof(profile));

        var plane0 = CreatePacket(0);
        var plane1 = CreatePacket(1);

        for (var z = 0; z < scene.Height; z++)
        for (var y = 0; y < scene.Depth; y++)
        for (var x = 0; x < scene.Width; x++)
        {
            var (width, depth, height) = profile.Orientation.Map(
                x, y, z, scene.Width, scene.Depth, scene.Height);
            var color = scene[x, y, z];
            EncodeChannel(plane0, plane1, height, width, depth, channelOffset: 0, color.G);
            EncodeChannel(plane0, plane1, height, width, depth, channelOffset: 2, color.B);
            EncodeChannel(plane0, plane1, height, width, depth, channelOffset: 4, color.R);
        }

        return new EncodedFrame([plane0, plane1], TimeSpan.Zero);
    }

    private static byte[] CreatePacket(byte plane)
    {
        var packet = new byte[PacketLength];
        packet[0] = 0x00;
        packet[1] = 0xFF;
        packet[2] = 0x00;
        packet[3] = 0x00;
        packet[4] = plane;
        return packet;
    }

    private static void EncodeChannel(
        byte[] plane0, byte[] plane1, int height, int width, int depth,
        int channelOffset, byte intensity)
    {
        if (intensity < MediumThreshold) return;

        var payloadByte = 24 * height + 6 * (width / 2) + channelOffset + width % 2;
        var mask = (byte)(1 << depth);
        plane0[PayloadOffset + payloadByte] |= mask;
        if (intensity >= HighThreshold)
            plane1[PayloadOffset + payloadByte] |= mask;
    }
}
