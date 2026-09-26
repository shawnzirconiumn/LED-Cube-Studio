namespace LedCube.Core.Devices;

public sealed record CubeDeviceProfile(
    string Id,
    string Name,
    int Width,
    int Depth,
    int Height,
    DeviceCapabilities Capabilities,
    int DefaultBaudRate,
    AxisOrientation Orientation,
    string? EncoderId,
    string ProtocolStatus,
    string? PreferredSerialIdentifier = null,
    TimeSpan? MinimumPacketStartInterval = null,
    TimeSpan? FrameSchedulerInterval = null,
    bool RestrictBaudToDefault = false)
{
    public bool IsProtocolSupported => EncoderId is not null;
    public override string ToString() => IsProtocolSupported ? Name : $"{Name} — protocol pending";
}
