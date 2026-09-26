using LedCube.Core.Audio;
using LedCube.Core.Diagnostics;
using NAudio.Wave;

namespace LedCube.Audio;

internal sealed class WindowsWasapiCaptureFactory : IAudioCaptureFactory
{
    private static readonly AudioSourceInfo DefaultSource = new(
        "wasapi-default-loopback",
        "Default system output (WASAPI loopback)",
        IsOutputMonitor: true,
        IsSystemDefault: true);

    public Task<IReadOnlyList<AudioSourceInfo>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<AudioSourceInfo>>([DefaultSource]);
    }

    public IAudioCapture Create(AudioSourceInfo source) => new WindowsWasapiCapture(source);
}

internal sealed class WindowsWasapiCapture(AudioSourceInfo source) : IAudioCapture
{
    private WasapiLoopbackCapture? _capture;

    public event EventHandler<AudioBuffer>? DataAvailable;
    public event EventHandler<string>? StatusChanged;
    public bool IsRunning { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsRunning) return Task.CompletedTask;
        var capture = new WasapiLoopbackCapture();
        capture.DataAvailable += OnDataAvailable;
        capture.RecordingStopped += OnRecordingStopped;
        _capture = capture;
        capture.StartRecording();
        IsRunning = true;
        StatusChanged?.Invoke(this, $"Capturing {source.DisplayName}: {capture.WaveFormat}");
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        var capture = Interlocked.Exchange(ref _capture, null);
        if (capture is not null)
        {
            capture.StopRecording();
            capture.DataAvailable -= OnDataAvailable;
            capture.RecordingStopped -= OnRecordingStopped;
            capture.Dispose();
        }
        IsRunning = false;
        StatusChanged?.Invoke(this, "Audio capture stopped.");
        return Task.CompletedTask;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var capture = _capture;
        if (capture is null) return;
        var format = capture.WaveFormat;
        var floating = format.Encoding == WaveFormatEncoding.IeeeFloat ||
                       format.Encoding == WaveFormatEncoding.Extensible && format.BitsPerSample == 32;
        try
        {
            var receivedTimestamp = PipelineClock.GetTimestamp();
            var decoded = PcmDecoder.Decode(e.Buffer, e.BytesRecorded, format.SampleRate,
                format.Channels, format.BitsPerSample, floating) with
            {
                AudioDataTimestamp = receivedTimestamp,
                CaptureReceivedTimestamp = receivedTimestamp
            };
            DataAvailable?.Invoke(this, decoded);
        }
        catch (Exception exception)
        {
            StatusChanged?.Invoke(this, $"Audio decode error: {exception.Message}");
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        IsRunning = false;
        StatusChanged?.Invoke(this, e.Exception is null ? "Audio capture stopped." : $"Audio capture failed: {e.Exception.Message}");
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
