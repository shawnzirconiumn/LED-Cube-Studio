using LedCube.Core.Devices;

namespace LedCube.Hardware.Transport;

/// <summary>
/// Optional transport-only pacing for an encoded frame. A null minimum preserves the
/// delay supplied by the encoder; otherwise packet starts are separated by the stated interval.
/// </summary>
public sealed record FrameTransportPacing(TimeSpan? MinimumPacketStartInterval = null)
{
    public static FrameTransportPacing EncoderDefault { get; } = new();

    public static FrameTransportPacing ForProfile(CubeDeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new FrameTransportPacing(profile.MinimumPacketStartInterval);
    }

    public static FrameTransportPacing ForWireTime(
        int packetBytes,
        int baudRate,
        TimeSpan processingGuard)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(packetBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baudRate);
        if (processingGuard < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(processingGuard));

        // 8N1 consumes ten wire bits per byte: start + 8 data + stop.
        var wireTime = TimeSpan.FromSeconds(packetBytes * 10d / baudRate);
        return new FrameTransportPacing(wireTime + processingGuard);
    }
}
