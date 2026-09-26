using LedCube.Core.Devices;
using LedCube.Core.Scene;
using LedCube.Hardware.Encoding;

namespace LedCube.Tests;

public sealed class Rgb8x8x8FrameEncoderTests
{
    private static readonly CubeDeviceProfile Profile = DeviceProfiles.All.Single(
        profile => profile.Id == DeviceProfiles.Rgb8x8x8ProfileId);

    [Fact]
    public void BlackSceneProducesTwoEmptyPayloadsWithProtocolHeaders()
    {
        var frame = Encode(new VoxelScene(8, 8, 8));

        Assert.Equal(2, frame.Packets.Count);
        Assert.Equal(394, frame.TotalBytes);
        for (var plane = 0; plane < 2; plane++)
        {
            var packet = frame.Packets[plane].ToArray();
            Assert.Equal(197, packet.Length);
            Assert.Equal(new byte[] { 0x00, 0xFF, 0x00, 0x00 }, packet[..4]);
            Assert.Equal(plane, packet[4]);
            Assert.All(packet[5..], value => Assert.Equal(0, value));
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 255, 0, 0, 0)]
    [InlineData(0, 7, 0, 0, 255, 0, 0, 7)]
    [InlineData(1, 0, 0, 0, 255, 0, 1, 0)]
    [InlineData(0, 0, 0, 0, 0, 255, 2, 0)]
    [InlineData(0, 0, 0, 255, 0, 0, 4, 0)]
    [InlineData(7, 0, 0, 255, 0, 0, 23, 0)]
    [InlineData(0, 0, 1, 0, 255, 0, 24, 0)]
    [InlineData(7, 7, 7, 255, 0, 0, 191, 7)]
    public void MapsObservedCoordinatesAndChannels(
        int x, int y, int z, byte red, byte green, byte blue,
        int expectedPayloadByte, int expectedBit)
    {
        var scene = new VoxelScene(8, 8, 8);
        scene[x, y, z] = VoxelColor.FromRgb(red, green, blue);

        var frame = Encode(scene);

        AssertSinglePayloadBit(frame.Packets[0].ToArray(), expectedPayloadByte, expectedBit);
        AssertSinglePayloadBit(frame.Packets[1].ToArray(), expectedPayloadByte, expectedBit);
    }

    [Fact]
    public void MediumIntensityActivatesOnlyPlaneZero()
    {
        var scene = new VoxelScene(8, 8, 8);
        scene[0, 0, 0] = VoxelColor.FromRgb(0, 128, 0);

        var frame = Encode(scene);

        AssertSinglePayloadBit(frame.Packets[0].ToArray(), payloadByte: 0, bit: 0);
        Assert.All(frame.Packets[1].ToArray()[5..], value => Assert.Equal(0, value));
    }

    [Fact]
    public void HighIntensityActivatesBothPlanes()
    {
        var scene = new VoxelScene(8, 8, 8);
        scene[0, 0, 0] = VoxelColor.FromRgb(0, 255, 0);

        var frame = Encode(scene);

        AssertSinglePayloadBit(frame.Packets[0].ToArray(), payloadByte: 0, bit: 0);
        AssertSinglePayloadBit(frame.Packets[1].ToArray(), payloadByte: 0, bit: 0);
    }

    [Fact]
    public void QuantizesRgbChannelsIndependentlyAtExactThresholds()
    {
        var scene = new VoxelScene(8, 8, 8);
        scene[0, 0, 0] = VoxelColor.FromRgb(red: 192, green: 63, blue: 64);

        var frame = Encode(scene);
        var plane0 = frame.Packets[0].ToArray();
        var plane1 = frame.Packets[1].ToArray();

        Assert.Equal(0, plane0[5 + 0]);
        Assert.Equal(1, plane0[5 + 2]);
        Assert.Equal(1, plane0[5 + 4]);
        Assert.Equal(0, plane1[5 + 0]);
        Assert.Equal(0, plane1[5 + 2]);
        Assert.Equal(1, plane1[5 + 4]);
        Assert.Equal(2, plane0[5..].Count(value => value != 0));
        Assert.Equal(1, plane1[5..].Count(value => value != 0));
    }

    private static EncodedFrame Encode(VoxelScene scene) =>
        new Rgb8x8x8FrameEncoder().Encode(scene, Profile);

    private static void AssertSinglePayloadBit(byte[] packet, int payloadByte, int bit)
    {
        Assert.Equal(1 << bit, packet[5 + payloadByte]);
        Assert.Equal(1, packet[5..].Count(value => value != 0));
    }
}
