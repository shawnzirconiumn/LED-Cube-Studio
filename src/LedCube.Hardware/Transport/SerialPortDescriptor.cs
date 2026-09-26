namespace LedCube.Hardware.Transport;

public sealed record SerialPortDescriptor(string Path, string DisplayName, bool IsStable = false)
{
    public override string ToString() => DisplayName;
}
