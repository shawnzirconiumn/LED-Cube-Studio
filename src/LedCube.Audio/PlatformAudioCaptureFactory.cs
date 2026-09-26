namespace LedCube.Audio;

public static class PlatformAudioCaptureFactory
{
    public static IAudioCaptureFactory Create()
    {
        if (OperatingSystem.IsWindows()) return new WindowsWasapiCaptureFactory();
        if (OperatingSystem.IsLinux()) return new LinuxPulseCaptureFactory();
        throw new PlatformNotSupportedException("Audio capture is currently implemented for Windows and Linux.");
    }
}
