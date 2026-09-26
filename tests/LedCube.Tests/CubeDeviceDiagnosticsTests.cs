using LedCube.Core.Devices;
using LedCube.Core.Scene;
using LedCube.Hardware;
using LedCube.Hardware.Encoding;
using LedCube.Hardware.Transport;

namespace LedCube.Tests;

public sealed class CubeDeviceDiagnosticsTests
{
    [Fact]
    public async Task ReportsEncodingAndCompletedFrameTransmissionSeparately()
    {
        await using var transport = new RecordingTransport();
        var device = new CubeDevice(transport, [new Rgb8x8x8FrameEncoder()]);
        FrameEncodedEventArgs? encoded = null;
        FrameSentEventArgs? sent = null;
        device.FrameEncoded += (_, args) => encoded = args;
        device.FrameSent += (_, args) => sent = args;
        await device.ConnectAsync("test", 115200);

        var scene = new VoxelScene(8, 8, 8);
        scene.Clear(VoxelColor.Red);
        await device.SendAsync(scene, DeviceProfiles.All[0]);

        Assert.NotNull(encoded);
        Assert.Equal(394, encoded.TotalBytes);
        Assert.Equal(2, encoded.PacketCount);
        Assert.NotNull(sent);
        Assert.Equal(394, sent.TotalBytes);
        Assert.Equal(2, sent.PacketCount);
        Assert.True(encoded.CompletedTimestamp > 0);
        Assert.True(sent.CompletedTimestamp >= encoded.CompletedTimestamp);
        Assert.Equal(new[] { 197, 197 }, transport.WriteSizes);
    }

    [Fact]
    public async Task DoesNotReportFrameAsSentWhenAPacketWriteFails()
    {
        await using var transport = new RecordingTransport(failOnWriteNumber: 2);
        var device = new CubeDevice(transport, [new Rgb8x8x8FrameEncoder()]);
        var encodedCount = 0;
        var sentCount = 0;
        device.FrameEncoded += (_, _) => encodedCount++;
        device.FrameSent += (_, _) => sentCount++;
        await device.ConnectAsync("test", 115200);

        await Assert.ThrowsAsync<IOException>(() =>
            device.SendAsync(new VoxelScene(8, 8, 8), DeviceProfiles.All[0]));

        Assert.Equal(1, encodedCount);
        Assert.Equal(0, sentCount);
        Assert.Equal(new[] { 197, 197 }, transport.WriteSizes);
    }

    private sealed class RecordingTransport : ISerialTransport
    {
        private readonly int? _failOnWriteNumber;

        public RecordingTransport(int? failOnWriteNumber = null) => _failOnWriteNumber = failOnWriteNumber;

        public bool IsOpen { get; private set; }
        public string? PortName { get; private set; }
        public List<int> WriteSizes { get; } = [];

        public Task OpenAsync(string portName, int baudRate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PortName = portName;
            IsOpen = true;
            return Task.CompletedTask;
        }

        public Task WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteSizes.Add(bytes.Length);
            if (WriteSizes.Count == _failOnWriteNumber) throw new IOException("Simulated serial write failure.");
            return Task.CompletedTask;
        }

        public Task CloseAsync()
        {
            IsOpen = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
