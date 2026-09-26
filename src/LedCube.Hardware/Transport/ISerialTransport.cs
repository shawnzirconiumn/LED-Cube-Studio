namespace LedCube.Hardware.Transport;

public interface ISerialTransport : IAsyncDisposable
{
    bool IsOpen { get; }
    string? PortName { get; }
    Task OpenAsync(string portName, int baudRate, CancellationToken cancellationToken = default);
    Task WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default);
    Task CloseAsync();
}
