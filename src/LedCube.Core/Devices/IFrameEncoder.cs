using LedCube.Core.Scene;

namespace LedCube.Core.Devices;

public sealed record EncodedFrame(IReadOnlyList<ReadOnlyMemory<byte>> Packets, TimeSpan InterPacketDelay)
{
    public int TotalBytes => Packets.Sum(packet => packet.Length);
}

public interface IFrameEncoder
{
    string Id { get; }
    EncodedFrame Encode(VoxelScene scene, CubeDeviceProfile profile);
}
