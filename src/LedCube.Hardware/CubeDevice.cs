using LedCube.Core.Devices;
using LedCube.Core.Diagnostics;
using LedCube.Core.Scene;
using LedCube.Hardware.Transport;

namespace LedCube.Hardware;

public sealed class CubeDevice
{
    private readonly ISerialTransport _transport;
    private readonly IReadOnlyDictionary<string, IFrameEncoder> _encoders;

    public CubeDevice(ISerialTransport transport, IEnumerable<IFrameEncoder> encoders)
    {
        _transport = transport;
        _encoders = encoders.ToDictionary(encoder => encoder.Id, StringComparer.Ordinal);
    }

    public bool IsConnected => _transport.IsOpen;

    public event EventHandler<FrameEncodedEventArgs>? FrameEncoded;
    public event EventHandler<FrameSentEventArgs>? FrameSent;

    public Task ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken = default) =>
        _transport.OpenAsync(portName, baudRate, cancellationToken);

    public Task DisconnectAsync() => _transport.CloseAsync();

    public async Task SendAsync(VoxelScene scene, CubeDeviceProfile profile,
        CancellationToken cancellationToken = default,
        FrameTransportPacing? pacing = null)
    {
        if (profile.EncoderId is null || !_encoders.TryGetValue(profile.EncoderId, out var encoder))
            throw new NotSupportedException(profile.ProtocolStatus);
        var effectivePacing = pacing ?? FrameTransportPacing.ForProfile(profile);
        var frame = encoder.Encode(scene, profile);
        FrameEncoded?.Invoke(this, new FrameEncodedEventArgs(
            frame.TotalBytes, frame.Packets.Count, PipelineClock.GetTimestamp()));
        for (var i = 0; i < frame.Packets.Count; i++)
        {
            var writeStartedTimestamp = PipelineClock.GetTimestamp();
            await _transport.WriteAsync(frame.Packets[i], cancellationToken).ConfigureAwait(false);
            if (i + 1 >= frame.Packets.Count) continue;

            if (effectivePacing.MinimumPacketStartInterval is { } minimumStartInterval)
            {
                var elapsed = PipelineClock.Elapsed(writeStartedTimestamp, PipelineClock.GetTimestamp());
                var remaining = minimumStartInterval - elapsed;
                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
            }
            else if (frame.InterPacketDelay > TimeSpan.Zero)
            {
                // Preserves the known-working baseline exactly: delay after write completion.
                await Task.Delay(frame.InterPacketDelay, cancellationToken).ConfigureAwait(false);
            }
        }
        FrameSent?.Invoke(this, new FrameSentEventArgs(
            frame.TotalBytes, frame.Packets.Count, PipelineClock.GetTimestamp()));
    }
}

public sealed class FrameEncodedEventArgs(int totalBytes, int packetCount, long completedTimestamp) : EventArgs
{
    public int TotalBytes { get; } = totalBytes;
    public int PacketCount { get; } = packetCount;
    public long CompletedTimestamp { get; } = completedTimestamp;
}

public sealed class FrameSentEventArgs(int totalBytes, int packetCount, long completedTimestamp) : EventArgs
{
    public int TotalBytes { get; } = totalBytes;
    public int PacketCount { get; } = packetCount;
    public long CompletedTimestamp { get; } = completedTimestamp;
}
