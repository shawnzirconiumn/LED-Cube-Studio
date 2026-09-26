namespace LedCube.Core.Devices;

public static class DeviceProfiles
{
    public const string Rgb8x8x8ProfileId = "rgb-8x8x8";
    public const string Rgb8x8x8EncoderId = "rgb-8x8x8-v1";
    public const int Rgb8x8x8ValidatedBaudRate = 115200;
    public const int Rgb8x8x8PacketBytes = 197;

    // 8N1 uses ten wire bits per byte. Physical testing established that one
    // additional millisecond safely separates the two packet starts at 115200.
    public static TimeSpan Rgb8x8x8MinimumPacketStartInterval { get; } =
        TimeSpan.FromSeconds(Rgb8x8x8PacketBytes * 10d / Rgb8x8x8ValidatedBaudRate) +
        TimeSpan.FromMilliseconds(1);
    public static TimeSpan Rgb8x8x8FrameSchedulerInterval { get; } =
        TimeSpan.FromMilliseconds(55);

    public static IReadOnlyList<CubeDeviceProfile> All { get; } =
    [
        new(Rgb8x8x8ProfileId, "8x8x8 RGB", 8, 8, 8, DeviceCapabilities.Rgb,
            Rgb8x8x8ValidatedBaudRate,
            AxisOrientation.Identity, Rgb8x8x8EncoderId,
            "Validated: two 197-byte color-depth packets at 115200 8N1 with optimized pacing.",
            MinimumPacketStartInterval: Rgb8x8x8MinimumPacketStartInterval,
            FrameSchedulerInterval: Rgb8x8x8FrameSchedulerInterval,
            RestrictBaudToDefault: true),
        new("rgb-8x8x32", "8x8x32 RGB", 8, 8, 32, DeviceCapabilities.Rgb, 115200,
            AxisOrientation.Identity, null,
            "Unsupported: firmware and packet layout must be inspected before an encoder is added."),
        new("mono-16x16x16", "16x16x16 Mono", 16, 16, 16, DeviceCapabilities.MonochromeIntensity,
            115200, AxisOrientation.Identity, null,
            "Unsupported: firmware and packet layout must be inspected before an encoder is added.")
    ];
}
