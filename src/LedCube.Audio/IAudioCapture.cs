using LedCube.Core.Audio;

namespace LedCube.Audio;

public interface IAudioCapture : IAsyncDisposable
{
    event EventHandler<AudioBuffer>? DataAvailable;
    event EventHandler<string>? StatusChanged;
    bool IsRunning { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}

public interface IAudioCaptureFactory
{
    Task<IReadOnlyList<AudioSourceInfo>> GetSourcesAsync(CancellationToken cancellationToken = default);
    IAudioCapture Create(AudioSourceInfo source);
}
