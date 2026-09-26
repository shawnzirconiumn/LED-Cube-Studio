using System.Diagnostics;
using System.IO.Ports;
using LedCube.Core.Diagnostics;

namespace LedCube.Hardware.Transport;

public sealed class SerialPortTransport : ISerialTransport
{
    private SerialPort? _port;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private long _lastWriteTimestamp;

    public bool IsOpen => _port?.IsOpen == true;
    public string? PortName => _port?.PortName;

    public event EventHandler<SerialWriteCompletedEventArgs>? WriteCompleted;
    public event EventHandler<SerialTransportErrorEventArgs>? ErrorReceived;

    public Task OpenAsync(string portName, int baudRate, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsOpen) throw new InvalidOperationException("The serial transport is already open.");
        var port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            DtrEnable = false,
            RtsEnable = false,
            WriteTimeout = 1000
        };
        port.Open();
        port.ErrorReceived += PortOnErrorReceived;
        _port = port;
        Interlocked.Exchange(ref _lastWriteTimestamp, 0);
        return Task.CompletedTask;
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        var port = _port;
        if (port?.IsOpen != true) throw new InvalidOperationException("The serial transport is not open.");
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var writeTimestamp = PipelineClock.GetTimestamp();
            var previousWriteTimestamp = Interlocked.Exchange(ref _lastWriteTimestamp, writeTimestamp);
            var startedAt = DateTimeOffset.Now;
            await port.BaseStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await port.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            var completedTimestamp = PipelineClock.GetTimestamp();
            var interval = previousWriteTimestamp == 0
                ? (TimeSpan?)null
                : PipelineClock.Elapsed(previousWriteTimestamp, writeTimestamp);
            WriteCompleted?.Invoke(this, new SerialWriteCompletedEventArgs(
                bytes.Length, startedAt, interval, writeTimestamp, completedTimestamp));
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public Task CloseAsync()
    {
        Interlocked.Exchange(ref _lastWriteTimestamp, 0);
        var port = Interlocked.Exchange(ref _port, null);
        if (port is not null)
        {
            port.ErrorReceived -= PortOnErrorReceived;
            if (port.IsOpen) port.Close();
            port.Dispose();
        }
        return Task.CompletedTask;
    }

    private void PortOnErrorReceived(object sender, SerialErrorReceivedEventArgs args) =>
        ErrorReceived?.Invoke(this, new SerialTransportErrorEventArgs(
            args.EventType.ToString(), PipelineClock.GetTimestamp()));

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        _writeLock.Dispose();
    }
}

public sealed class SerialTransportErrorEventArgs(string error, long timestamp) : EventArgs
{
    public string Error { get; } = error;
    public long Timestamp { get; } = timestamp;
}

public sealed class SerialWriteCompletedEventArgs(
    int byteCount,
    DateTimeOffset startedAt,
    TimeSpan? intervalSincePreviousWrite,
    long startedTimestamp,
    long completedTimestamp) : EventArgs
{
    public int ByteCount { get; } = byteCount;
    public DateTimeOffset StartedAt { get; } = startedAt;
    public TimeSpan? IntervalSincePreviousWrite { get; } = intervalSincePreviousWrite;
    public long StartedTimestamp { get; } = startedTimestamp;
    public long CompletedTimestamp { get; } = completedTimestamp;
}
