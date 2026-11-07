using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using LedCube.App.Presentation;
using LedCube.App.Settings;
using LedCube.Audio;
using LedCube.Core.Audio;
using LedCube.Core.Devices;
using LedCube.Core.Diagnostics;
using LedCube.Core.Scene;
using LedCube.Core.Visualization;
using LedCube.Core.Visualization.Builder;
using LedCube.Hardware;
using LedCube.Hardware.Encoding;
using LedCube.Hardware.Transport;

namespace LedCube.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private const int MaximumDiagnosticLogLines = 160;
    private static readonly IReadOnlyList<int> GeneralBaudRates =
        [9600, 57600, 115200, 230400, 460800, 2_000_000];
    private static readonly bool MirrorDiagnosticsToStandardOutput = string.Equals(
        Environment.GetEnvironmentVariable("LEDCUBE_DIAGNOSTICS_STDOUT"), "1", StringComparison.Ordinal);
    private readonly SpectrumAnalyzer _analyzer = new();
    private readonly IAudioCaptureFactory _audioFactory;
    private readonly IApplicationSettingsStore _settingsStore;
    private readonly SerialPortTransport _transport = new();
    private readonly CubeDevice _device;
    private readonly Queue<string> _diagnosticLogLines = new();
    private readonly Stopwatch _rateClock = new();
    private readonly object _audioDiagnosticSync = new();
    private readonly Stopwatch _audioDiagnosticClock = new();
    private readonly RollingFrameTimingWindow _frameTimingWindow = new(10);
    private CubeDeviceProfile _selectedProfile;
    private SerialPortDescriptor? _selectedSerialPort;
    private AudioSourceInfo? _selectedAudioSource;
    private IVisualizer _selectedVisualizer;
    private IVisualizer? _visualizerBeforePreview;
    private StreamModeOption _selectedStreamMode;
    private int _selectedBaudRate;
    private double _sensitivity = 100;
    private double _decay = 15;
    private string _connectionStatus = "Disconnected";
    private string _audioStatus = "Audio capture is stopped.";
    private string _detailStatus;
    private string _userStatus = "Configure a device and connect to begin streaming.";
    private string _diagnosticLog = "Hardware-path telemetry appears here while connected.";
    private string _audioPipelineDiagnostic = "Audio pipeline diagnostic appears here in audio mode.";
    private string _endToEndAudioAge = "End-to-end audio age: waiting for a complete audio frame…";
    private bool _diagnosticsVisible;
    private bool _deviceSetupExpanded = true;
    private IAudioCapture? _capture;
    private CancellationTokenSource? _renderCancellation;
    private Task? _renderTask;
    private long _encodedFramesInWindow;
    private long _sentFramesInWindow;
    private long _bytesWrittenInWindow;
    private long _encodedFrameSequence;
    private long _sentFrameSequence;
    private long _serialWriteSequence;
    private long _serialErrorTotal;
    private long _audioCallbackTotal;
    private long _audioCallbackWindow;
    private long _audioBytesTotal;
    private long _audioBytesWindow;
    private long _audioSampleFramesTotal;
    private long _audioSampleFramesWindow;
    private long _fftWindowTotal;
    private long _fftWindowWindow;
    private long _rmsSampleFramesWindow;
    private double _leftSquaresWindow;
    private double _rightSquaresWindow;
    private int _latestSampleRate;
    private long _renderedFramesWindow;
    private int _pendingAudioBlocks;
    private int _pendingRenderFrames;
    private int _pendingSerialFrames;
    private long _oldestPendingAudioTimestamp;
    private FramePipelineTrace? _activePipelineFrame;
    private FramePipelineTrace? _lastCompletedPipelineFrame;

    public MainWindowViewModel() : this(new JsonApplicationSettingsStore())
    {
    }

    public MainWindowViewModel(IApplicationSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        _audioFactory = PlatformAudioCaptureFactory.Create();
        _device = new CubeDevice(_transport, [new Rgb8x8x8FrameEncoder()]);
        Profiles = DeviceProfiles.All;
        _selectedProfile = Profiles[0];
        Builder = new VisualizerBuilderViewModel();
        Builder.PreviewRequested += BuilderOnPreviewRequested;
        Builder.PreviewStopped += BuilderOnPreviewStopped;
        Visualizers = VisualizerRegistry.CreateAll().Concat<IVisualizer>([Builder.Runtime]).ToArray();
        _selectedVisualizer = Visualizers[0];
        _visualizerBeforePreview = _selectedVisualizer;
        StreamModes =
        [
            new StreamModeOption("Audio visualizer", null),
            new StreamModeOption("Hardware test: solid red", VoxelColor.Red),
            new StreamModeOption("Hardware test: solid green", VoxelColor.Green),
            new StreamModeOption("Hardware test: solid blue", VoxelColor.Blue)
        ];
        _selectedStreamMode = StreamModes[0];
        _selectedBaudRate = _selectedProfile.DefaultBaudRate;
        _detailStatus = _selectedProfile.ProtocolStatus;
        ToggleConnectionCommand = new AsyncCommand(ToggleConnectionAsync);
        RefreshPortsCommand = new RelayCommand(RefreshPorts);
        RefreshAudioSourcesCommand = new AsyncCommand(RefreshAudioSourcesAsync);
        _device.FrameEncoded += DeviceOnFrameEncoded;
        _device.FrameSent += DeviceOnFrameSent;
        _transport.WriteCompleted += TransportOnWriteCompleted;
        _transport.ErrorReceived += TransportOnErrorReceived;
        _analyzer.SnapshotAvailable += AnalyzerOnSnapshotAvailable;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<CubeDeviceProfile> Profiles { get; }
    public ObservableCollection<SerialPortDescriptor> SerialPorts { get; } = [];
    public ObservableCollection<AudioSourceInfo> AudioSources { get; } = [];
    public IReadOnlyList<IVisualizer> Visualizers { get; }
    public VisualizerBuilderViewModel Builder { get; }
    public IReadOnlyList<StreamModeOption> StreamModes { get; }
    public IReadOnlyList<int> BaudRates =>
        SettingsSelection.AvailableBaudRates(SelectedProfile, GeneralBaudRates);
    public AsyncCommand ToggleConnectionCommand { get; }
    public RelayCommand RefreshPortsCommand { get; }
    public AsyncCommand RefreshAudioSourcesCommand { get; }

    public CubeDeviceProfile SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!Set(ref _selectedProfile, value)) return;
            OnPropertyChanged(nameof(BaudRates));
            SelectedBaudRate = value.DefaultBaudRate;
            DetailStatus = value.ProtocolStatus;
            OnPropertyChanged(nameof(ConnectionSummary));
        }
    }

    public SerialPortDescriptor? SelectedSerialPort
    {
        get => _selectedSerialPort;
        set
        {
            if (Set(ref _selectedSerialPort, value))
                OnPropertyChanged(nameof(ConnectionSummary));
        }
    }

    public AudioSourceInfo? SelectedAudioSource
    {
        get => _selectedAudioSource;
        set
        {
            if (Set(ref _selectedAudioSource, value))
            {
                OnPropertyChanged(nameof(ConnectionSourceSummary));
                OnPropertyChanged(nameof(HasNoAudioSource));
            }
        }
    }

    public IVisualizer SelectedVisualizer
    {
        get => _selectedVisualizer;
        set
        {
            if (!Set(ref _selectedVisualizer, value)) return;
            if (ReferenceEquals(value, Builder.Runtime)) Builder.SetPreviewActiveFromHost(true);
            else
            {
                _visualizerBeforePreview = value;
                Builder.SetPreviewActiveFromHost(false);
            }
            if (_device.IsConnected && SelectedStreamMode.UsesAudio)
            {
                DetailStatus = $"Streaming {value.DisplayName} to {SelectedProfile.Name}. Visualizer and response controls update live.";
                UserStatus = $"Streaming {value.DisplayName} to {SelectedProfile.Name}";
            }
        }
    }

    public StreamModeOption SelectedStreamMode
    {
        get => _selectedStreamMode;
        set
        {
            if (Set(ref _selectedStreamMode, value))
                OnPropertyChanged(nameof(ConnectionSourceSummary));
        }
    }

    public int SelectedBaudRate
    {
        get => _selectedBaudRate;
        set
        {
            if (Set(ref _selectedBaudRate, value))
                OnPropertyChanged(nameof(ConnectionSummary));
        }
    }

    public double Sensitivity
    {
        get => _sensitivity;
        set
        {
            var clamped = Math.Clamp(double.IsFinite(value) ? value : ApplicationSettings.DefaultSensitivity, 0, 200);
            if (!Set(ref _sensitivity, clamped)) return;
            _analyzer.SensitivityPercent = (int)Math.Round(clamped);
            OnPropertyChanged(nameof(SensitivityLabel));
        }
    }

    public double Decay
    {
        get => _decay;
        set
        {
            var clamped = Math.Clamp(double.IsFinite(value) ? value : ApplicationSettings.DefaultDecay, 0, 100);
            if (!Set(ref _decay, clamped)) return;
            _analyzer.DecayPercent = (int)Math.Round(clamped);
            OnPropertyChanged(nameof(DecayLabel));
        }
    }

    public string SensitivityLabel => $"{Sensitivity:0}%";
    public string DecayLabel => $"{Decay:0}%";
    public string ConnectionButtonText => _device.IsConnected ? "Disconnect" : "Connect";
    public bool IsConnectionConfigurationEnabled => !_device.IsConnected;
    public bool IsConnected => _device.IsConnected;
    public bool IsDisconnected => !_device.IsConnected;
    public int SetupSectionRow => IsConnected ? 4 : 1;
    public int VisualizerSectionRow => IsConnected ? 1 : 2;
    public bool HasNoSerialPorts => SerialPorts.Count == 0;
    public bool HasNoAudioSource => SelectedAudioSource is null;
    public string ConnectionStateLabel => ConnectionStatus switch
    {
        var status when status.StartsWith("Connected", StringComparison.Ordinal) => "Connected",
        var status when status.StartsWith("Connecting", StringComparison.Ordinal) => "Connecting…",
        var status when status.Contains("failed", StringComparison.OrdinalIgnoreCase) => "Connection error",
        _ => "Disconnected"
    };
    public string ConnectionSummary =>
        $"{SelectedProfile.Name}  •  {FriendlyNames.SerialPort(SelectedSerialPort)}  •  {SelectedBaudRate}";
    public string ConnectionSourceSummary => SelectedStreamMode.UsesAudio
        ? FriendlyNames.AudioSource(SelectedAudioSource)
        : SelectedStreamMode.Name;

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set
        {
            if (Set(ref _connectionStatus, value))
                OnPropertyChanged(nameof(ConnectionStateLabel));
        }
    }

    public string AudioStatus
    {
        get => _audioStatus;
        private set => Set(ref _audioStatus, value);
    }

    public string DetailStatus
    {
        get => _detailStatus;
        private set => Set(ref _detailStatus, value);
    }

    public string UserStatus
    {
        get => _userStatus;
        private set => Set(ref _userStatus, value);
    }

    public string DiagnosticLog
    {
        get => _diagnosticLog;
        private set => Set(ref _diagnosticLog, value);
    }

    public string AudioPipelineDiagnostic
    {
        get => _audioPipelineDiagnostic;
        private set => Set(ref _audioPipelineDiagnostic, value);
    }

    public string EndToEndAudioAge
    {
        get => _endToEndAudioAge;
        private set => Set(ref _endToEndAudioAge, value);
    }

    public bool DiagnosticsVisible
    {
        get => _diagnosticsVisible;
        set => Set(ref _diagnosticsVisible, value);
    }

    public bool DeviceSetupExpanded
    {
        get => _deviceSetupExpanded;
        set => Set(ref _deviceSetupExpanded, value);
    }

    public async Task InitializeAsync()
    {
        await Builder.InitializeAsync().ConfigureAwait(true);
        var settings = await _settingsStore.LoadAsync().ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() => ApplySettings(settings));
        await RefreshAudioSourcesAsync(settings.SelectedAudioSourceId).ConfigureAwait(false);
    }

    private void BuilderOnPreviewRequested(CustomVisualizerRuntime runtime)
    {
        if (!ReferenceEquals(SelectedVisualizer, runtime)) _visualizerBeforePreview = SelectedVisualizer;
        SelectedVisualizer = runtime;
        DetailStatus = IsConnected
            ? $"Live-previewing {runtime.DisplayName}; builder edits update immediately."
            : $"{runtime.DisplayName} is ready. Connect the cube to see the live preview.";
        UserStatus = DetailStatus;
    }

    private void BuilderOnPreviewStopped()
    {
        SelectedVisualizer = _visualizerBeforePreview
                             ?? Visualizers.First(visualizer => !ReferenceEquals(visualizer, Builder.Runtime));
        DetailStatus = IsConnected
            ? $"Streaming {SelectedVisualizer.DisplayName} to {SelectedProfile.Name}."
            : $"Preview stopped. {SelectedVisualizer.DisplayName} is selected.";
        UserStatus = DetailStatus;
    }

    private Task RefreshAudioSourcesAsync() =>
        RefreshAudioSourcesAsync(SelectedAudioSource?.Id);

    private async Task RefreshAudioSourcesAsync(string? preferredSourceId)
    {
        try
        {
            var sources = await _audioFactory.GetSourcesAsync().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AudioSources.Clear();
                foreach (var source in sources) AudioSources.Add(source);
                SelectedAudioSource = AudioSourceSelection.SelectPreferred(
                    AudioSources.ToArray(), preferredSourceId);
                AudioStatus = SelectedAudioSource is null
                    ? "No output-monitor audio source was found."
                    : $"Audio source ready: {SelectedAudioSource.DisplayName}";
            });
        }
        catch (Exception exception)
        {
            PostStatus($"Audio discovery failed: {exception.Message}", isAudio: true);
        }
    }

    private void ApplySettings(ApplicationSettings settings)
    {
        var sanitized = settings.Sanitize();
        SelectedProfile = SettingsSelection.ResolveAvailable(
                              Profiles, sanitized.SelectedProfileId, profile => profile.Id)
                          ?? Profiles[0];
        SelectedBaudRate = SettingsSelection.ResolveBaudRateForProfile(
            SelectedProfile, GeneralBaudRates, sanitized.BaudRate);
        SelectedVisualizer = SettingsSelection.ResolveAvailable(
                                 Visualizers, sanitized.SelectedVisualizerId, visualizer => visualizer.Id)
                             ?? Visualizers[0];
        Sensitivity = sanitized.Sensitivity;
        Decay = sanitized.Decay;
        DiagnosticsVisible = false;
        RefreshPorts(sanitized.SelectedSerialPath);
    }

    private void RefreshPorts() => RefreshPorts(null);

    public static SerialPortDescriptor? SelectPreferredSerialPort(
        IReadOnlyList<SerialPortDescriptor> ports,
        string? savedPath = null)
    {
        ArgumentNullException.ThrowIfNull(ports);
        var saved = ports.FirstOrDefault(port => port.Path == savedPath);
        if (saved is not null) return saved;
        if (ports.Count == 1) return ports[0];

        var stablePorts = ports.Where(port => port.IsStable).Take(2).ToArray();
        return stablePorts.Length == 1 ? stablePorts[0] : null;
    }

    private void RefreshPorts(string? preferredPath)
    {
        var selectedPath = preferredPath ?? SelectedSerialPort?.Path;
        SerialPorts.Clear();
        foreach (var port in SerialPortDiscovery.GetPorts()) SerialPorts.Add(port);
        OnPropertyChanged(nameof(HasNoSerialPorts));
        SelectedSerialPort = SelectPreferredSerialPort(SerialPorts, selectedPath);
        if (SerialPorts.Count == 0 && !_device.IsConnected)
            DetailStatus = $"{SelectedProfile.ProtocolStatus}{Environment.NewLine}No serial cube was detected. Check USB and permissions.";
    }

    private async Task ToggleConnectionAsync()
    {
        if (_device.IsConnected)
        {
            await DisconnectAsync().ConfigureAwait(true);
            return;
        }

        if (!SelectedProfile.IsProtocolSupported)
        {
            DetailStatus = SelectedProfile.ProtocolStatus;
            UserStatus = SelectedProfile.ProtocolStatus;
            return;
        }
        if (SelectedSerialPort is null)
        {
            DetailStatus = "Select a serial port before connecting.";
            UserStatus = DetailStatus;
            return;
        }
        var streamMode = SelectedStreamMode;
        if (streamMode.UsesAudio && SelectedAudioSource is null)
        {
            DetailStatus = "Select an audio source before connecting.";
            UserStatus = DetailStatus;
            return;
        }

        try
        {
            ConnectionStatus = "Connecting…";
            UserStatus = "Connecting to the LED cube…";
            SelectedBaudRate = SettingsSelection.ResolveBaudRateForProfile(
                SelectedProfile, GeneralBaudRates, SelectedBaudRate);
            await _device.ConnectAsync(SelectedSerialPort.Path, SelectedBaudRate).ConfigureAwait(true);
            ResetDiagnostics();
            ResetAudioPipelineDiagnostics(streamMode.UsesAudio);
            if (streamMode.UsesAudio)
            {
                _capture = _audioFactory.Create(SelectedAudioSource!);
                _capture.DataAvailable += CaptureOnDataAvailable;
                _capture.StatusChanged += CaptureOnStatusChanged;
                await _capture.StartAsync().ConfigureAwait(true);
            }
            else
            {
                AudioStatus = "Audio capture and FFT are bypassed by hardware test mode.";
            }
            _renderCancellation = new CancellationTokenSource();
            _renderTask = RenderLoopAsync(streamMode, _renderCancellation.Token);
            ConnectionStatus = $"Connected to {SelectedSerialPort.DisplayName} at {SelectedBaudRate} 8N1";
            DetailStatus = streamMode.UsesAudio
                ? $"Streaming {SelectedVisualizer.DisplayName} to {SelectedProfile.Name}. Visualizer and response controls update live."
                : $"Continuously streaming {streamMode.Name} to {SelectedProfile.Name}; no audio/FFT path is active.";
            UserStatus = streamMode.UsesAudio
                ? $"Streaming {SelectedVisualizer.DisplayName} to {SelectedProfile.Name}"
                : $"Streaming {streamMode.Name} to {SelectedProfile.Name}";
            DeviceSetupExpanded = false;
        }
        catch (Exception exception)
        {
            await DisconnectAsync().ConfigureAwait(true);
            ConnectionStatus = "Connection failed";
            DetailStatus = exception.Message;
            UserStatus = "Could not connect to the LED cube. Check the connection and try again.";
            DeviceSetupExpanded = true;
        }
        NotifyConnectionPresentation();
    }

    private void CaptureOnDataAvailable(object? sender, AudioBuffer buffer)
    {
        Interlocked.Increment(ref _pendingAudioBlocks);
        Interlocked.CompareExchange(ref _oldestPendingAudioTimestamp,
            buffer.AudioDataTimestamp, 0);
        double leftSquares = 0;
        double rightSquares = 0;
        for (var i = 0; i < buffer.SampleCount; i++)
        {
            leftSquares += buffer.Left[i] * buffer.Left[i];
            rightSquares += buffer.Right[i] * buffer.Right[i];
        }

        lock (_audioDiagnosticSync)
        {
            _audioCallbackTotal++;
            _audioCallbackWindow++;
            _audioBytesTotal += buffer.SourceByteCount;
            _audioBytesWindow += buffer.SourceByteCount;
            _audioSampleFramesTotal += buffer.SampleCount;
            _audioSampleFramesWindow += buffer.SampleCount;
            _rmsSampleFramesWindow += buffer.SampleCount;
            _leftSquaresWindow += leftSquares;
            _rightSquaresWindow += rightSquares;
            _latestSampleRate = buffer.SampleRate;
        }

        try
        {
            _analyzer.Process(buffer);
        }
        finally
        {
            if (Interlocked.Decrement(ref _pendingAudioBlocks) == 0)
                Interlocked.Exchange(ref _oldestPendingAudioTimestamp, 0);
        }
    }

    private void AnalyzerOnSnapshotAvailable(object? sender, AudioAnalysisSnapshot snapshot)
    {
        lock (_audioDiagnosticSync)
        {
            _fftWindowTotal++;
            _fftWindowWindow++;
        }
    }

    private void CaptureOnStatusChanged(object? sender, string status) => PostStatus(status, isAudio: true);

    private async Task RenderLoopAsync(
        StreamModeOption streamMode,
        CancellationToken cancellationToken)
    {
        var profile = SelectedProfile;
        var frameInterval = profile.FrameSchedulerInterval ?? TimeSpan.FromMilliseconds(50);
        var scene = new VoxelScene(profile.Width, profile.Depth, profile.Height);
        if (streamMode.StaticColor is { } staticColor) scene.Clear(staticColor);
        var clock = Stopwatch.StartNew();
        var previous = clock.Elapsed;
        long frame = 0;
        try
        {
            using var timer = new PeriodicTimer(frameInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var now = clock.Elapsed;
                AudioAnalysisSnapshot? renderedAnalysis = null;
                string? renderedVisualizerName = null;
                if (streamMode.UsesAudio)
                {
                    var analysis = _analyzer.Latest;
                    var visualizer = SelectedVisualizer;
                    renderedAnalysis = analysis;
                    renderedVisualizerName = visualizer.DisplayName;
                    Interlocked.Exchange(ref _pendingRenderFrames, 1);
                    var pipelineFrame = new FramePipelineTrace(analysis.PipelineTiming)
                    {
                        VisualizerStartedTimestamp = PipelineClock.GetTimestamp()
                    };
                    try
                    {
                        VisualizerRenderer.Render(visualizer, scene, analysis,
                            new VisualizationFrame(now, now - previous, frame++, profile.Capabilities),
                            _analyzer.SensitivityPercent);
                        pipelineFrame.SceneCompletedTimestamp = PipelineClock.GetTimestamp();
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _pendingRenderFrames, 0);
                    }
                    Interlocked.Increment(ref _renderedFramesWindow);
                    lock (_audioDiagnosticSync) _activePipelineFrame = pipelineFrame;
                }
                previous = now;
                Interlocked.Exchange(ref _pendingSerialFrames, 1);
                try
                {
                    await _device.SendAsync(scene, profile, cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Exchange(ref _pendingSerialFrames, 0);
                }
                if (renderedAnalysis is not null && renderedVisualizerName is not null)
                    LogAudioPipelineIfDue(scene, renderedAnalysis, renderedVisualizerName);
                LogRatesIfDue();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PostStatus($"Streaming stopped: {exception.Message}");
        }
    }

    private async Task DisconnectAsync()
    {
        var cancellation = Interlocked.Exchange(ref _renderCancellation, null);
        cancellation?.Cancel();
        var renderTask = Interlocked.Exchange(ref _renderTask, null);
        if (renderTask is not null)
        {
            try { await renderTask.ConfigureAwait(true); } catch (OperationCanceledException) { }
        }
        cancellation?.Dispose();
        LogRatesIfDue(force: true);

        var capture = Interlocked.Exchange(ref _capture, null);
        if (capture is not null)
        {
            capture.DataAvailable -= CaptureOnDataAvailable;
            capture.StatusChanged -= CaptureOnStatusChanged;
            await capture.DisposeAsync().ConfigureAwait(true);
        }
        await _device.DisconnectAsync().ConfigureAwait(true);
        ConnectionStatus = "Disconnected";
        AudioStatus = "Audio capture is stopped.";
        UserStatus = "Disconnected. Device and audio setup is ready.";
        DeviceSetupExpanded = true;
        NotifyConnectionPresentation();
    }

    private void NotifyConnectionPresentation()
    {
        OnPropertyChanged(nameof(IsConnectionConfigurationEnabled));
        OnPropertyChanged(nameof(ConnectionButtonText));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsDisconnected));
        OnPropertyChanged(nameof(SetupSectionRow));
        OnPropertyChanged(nameof(VisualizerSectionRow));
    }

    private void DeviceOnFrameEncoded(object? sender, FrameEncodedEventArgs args)
    {
        var sequence = Interlocked.Increment(ref _encodedFrameSequence);
        Interlocked.Increment(ref _encodedFramesInWindow);
        lock (_audioDiagnosticSync)
        {
            if (_activePipelineFrame is { } frame)
                frame.FrameEncodedTimestamp = args.CompletedTimestamp;
        }
        AppendDiagnostic($"encoded frame #{sequence}: {args.TotalBytes} bytes ({args.PacketCount} packets)");
    }

    private void DeviceOnFrameSent(object? sender, FrameSentEventArgs args)
    {
        var sequence = Interlocked.Increment(ref _sentFrameSequence);
        Interlocked.Increment(ref _sentFramesInWindow);
        lock (_audioDiagnosticSync)
        {
            if (_activePipelineFrame is { } frame)
            {
                if (frame.SecondWriteCompletedTimestamp == 0)
                    frame.SecondWriteCompletedTimestamp = args.CompletedTimestamp;
                _lastCompletedPipelineFrame = frame;
                if (frame.Audio.AudioDataTimestamp > 0 && frame.FirstWriteStartedTimestamp > 0)
                {
                    _frameTimingWindow.Add(
                        frame.SecondWriteCompletedTimestamp,
                        PipelineClock.Elapsed(frame.Audio.AudioDataTimestamp,
                            frame.SecondWriteCompletedTimestamp).TotalMilliseconds,
                        PipelineClock.Elapsed(frame.FirstWriteStartedTimestamp,
                            frame.SecondWriteCompletedTimestamp).TotalMilliseconds);
                }
                _activePipelineFrame = null;
            }
        }
        AppendDiagnostic($"sent frame #{sequence}: {args.TotalBytes} bytes");
    }

    private void TransportOnWriteCompleted(object? sender, SerialWriteCompletedEventArgs args)
    {
        var sequence = Interlocked.Increment(ref _serialWriteSequence);
        Interlocked.Add(ref _bytesWrittenInWindow, args.ByteCount);
        lock (_audioDiagnosticSync)
        {
            if (_activePipelineFrame is { } frame)
            {
                frame.SerialWriteCount++;
                if (frame.SerialWriteCount == 1)
                    frame.FirstWriteStartedTimestamp = args.StartedTimestamp;
                if (frame.SerialWriteCount == 2)
                    frame.SecondWriteCompletedTimestamp = args.CompletedTimestamp;
            }
        }
        var interval = args.IntervalSincePreviousWrite is { } elapsed
            ? $"{elapsed.TotalMilliseconds:0.000} ms since previous write"
            : "first write";
        AppendDiagnostic($"serial write #{sequence}: {args.ByteCount} bytes at " +
            $"{args.StartedAt:HH:mm:ss.fff zzz}; {interval}");
    }

    private void TransportOnErrorReceived(object? sender, SerialTransportErrorEventArgs args)
    {
        var count = Interlocked.Increment(ref _serialErrorTotal);
        AppendDiagnostic($"SERIAL ERROR #{count}: {args.Error}");
    }

    private void ResetDiagnostics()
    {
        Interlocked.Exchange(ref _encodedFramesInWindow, 0);
        Interlocked.Exchange(ref _sentFramesInWindow, 0);
        Interlocked.Exchange(ref _bytesWrittenInWindow, 0);
        Interlocked.Exchange(ref _encodedFrameSequence, 0);
        Interlocked.Exchange(ref _sentFrameSequence, 0);
        Interlocked.Exchange(ref _serialWriteSequence, 0);
        Interlocked.Exchange(ref _serialErrorTotal, 0);
        _rateClock.Restart();
        _diagnosticLogLines.Clear();
        DiagnosticLog = string.Empty;
        var cadence = SelectedProfile.FrameSchedulerInterval is { } interval
            ? 1 / interval.TotalSeconds
            : 20;
        AppendDiagnostic($"diagnostic counters reset; expected frame size 394 bytes and target cadence {cadence:0.0} FPS");
    }

    private void ResetAudioPipelineDiagnostics(bool enabled)
    {
        lock (_audioDiagnosticSync)
        {
            _audioCallbackTotal = 0;
            _audioCallbackWindow = 0;
            _audioBytesTotal = 0;
            _audioBytesWindow = 0;
            _audioSampleFramesTotal = 0;
            _audioSampleFramesWindow = 0;
            _fftWindowTotal = 0;
            _fftWindowWindow = 0;
            _rmsSampleFramesWindow = 0;
            _leftSquaresWindow = 0;
            _rightSquaresWindow = 0;
            _latestSampleRate = 0;
            _activePipelineFrame = null;
            _lastCompletedPipelineFrame = null;
            _frameTimingWindow.Reset();
        }
        Interlocked.Exchange(ref _renderedFramesWindow, 0);
        Interlocked.Exchange(ref _pendingAudioBlocks, 0);
        Interlocked.Exchange(ref _pendingRenderFrames, 0);
        Interlocked.Exchange(ref _pendingSerialFrames, 0);
        Interlocked.Exchange(ref _oldestPendingAudioTimestamp, 0);
        _audioDiagnosticClock.Restart();
        AudioPipelineDiagnostic = enabled
            ? "Waiting for the first 500 ms audio-pipeline sample…"
            : "Audio pipeline diagnostic is inactive because hardware test mode bypasses audio and FFT.";
        EndToEndAudioAge = enabled
            ? "End-to-end audio age: waiting for a complete audio frame…"
            : "End-to-end audio age: unavailable in hardware test mode";
    }

    private void LogAudioPipelineIfDue(
        VoxelScene scene,
        AudioAnalysisSnapshot analysis,
        string visualizerName)
    {
        var elapsed = _audioDiagnosticClock.Elapsed;
        if (elapsed < TimeSpan.FromMilliseconds(500)) return;

        long callbackTotal;
        long callbackWindow;
        long byteTotal;
        long byteWindow;
        long sampleFrameTotal;
        long sampleFrameWindow;
        long fftTotal;
        long fftWindow;
        long rmsSampleFrames;
        double leftSquares;
        double rightSquares;
        int sampleRate;
        FramePipelineTrace? pipelineFrame;
        RollingFrameTimingSummary frameTiming;
        var diagnosticTimestamp = PipelineClock.GetTimestamp();
        lock (_audioDiagnosticSync)
        {
            callbackTotal = _audioCallbackTotal;
            callbackWindow = _audioCallbackWindow;
            byteTotal = _audioBytesTotal;
            byteWindow = _audioBytesWindow;
            sampleFrameTotal = _audioSampleFramesTotal;
            sampleFrameWindow = _audioSampleFramesWindow;
            fftTotal = _fftWindowTotal;
            fftWindow = _fftWindowWindow;
            rmsSampleFrames = _rmsSampleFramesWindow;
            leftSquares = _leftSquaresWindow;
            rightSquares = _rightSquaresWindow;
            sampleRate = _latestSampleRate;
            pipelineFrame = _lastCompletedPipelineFrame;
            frameTiming = _frameTimingWindow.GetSummary(diagnosticTimestamp);

            _audioCallbackWindow = 0;
            _audioBytesWindow = 0;
            _audioSampleFramesWindow = 0;
            _fftWindowWindow = 0;
            _rmsSampleFramesWindow = 0;
            _leftSquaresWindow = 0;
            _rightSquaresWindow = 0;
        }
        _audioDiagnosticClock.Restart();
        var renderedFrames = Interlocked.Exchange(ref _renderedFramesWindow, 0);

        var seconds = elapsed.TotalSeconds;
        var leftRms = rmsSampleFrames > 0 ? Math.Sqrt(leftSquares / rmsSampleFrames) : 0;
        var rightRms = rmsSampleFrames > 0 ? Math.Sqrt(rightSquares / rmsSampleFrames) : 0;
        var bands = string.Join(", ", Enumerable.Range(0, SpectrumAnalyzer.BandCount).Select(index =>
            (index < analysis.Spectrum.Count ? analysis.Spectrum[index] : 0)
            .ToString("0.000", CultureInfo.InvariantCulture)));
        var sceneStatistics = scene.GetStatistics();
        var pendingAudio = Volatile.Read(ref _pendingAudioBlocks);
        var pendingRender = Volatile.Read(ref _pendingRenderFrames);
        var pendingSerial = Volatile.Read(ref _pendingSerialFrames);
        var oldestPendingTimestamp = Volatile.Read(ref _oldestPendingAudioTimestamp);
        var oldestPendingAge = oldestPendingTimestamp > 0
            ? PipelineClock.Elapsed(oldestPendingTimestamp, diagnosticTimestamp).TotalMilliseconds
            : 0;
        var latency = FormatPipelineLatency(pipelineFrame);
        var renderFps = renderedFrames / seconds;
        var headline = frameTiming.HasSamples
            ? $"End-to-end audio age: {frameTiming.CurrentAudioAgeMilliseconds:0.0} ms"
            : "End-to-end audio age: waiting for a complete audio frame…";
        var rollingSummary = frameTiming.HasSamples
            ? $"Audio age rolling {frameTiming.WindowSeconds} s: " +
              $"current={frameTiming.CurrentAudioAgeMilliseconds:0.0} ms; " +
              $"min={frameTiming.MinimumAudioAgeMilliseconds:0.0} ms; " +
              $"avg={frameTiming.AverageAudioAgeMilliseconds:0.0} ms; " +
              $"max={frameTiming.MaximumAudioAgeMilliseconds:0.0} ms"
            : $"Audio age rolling {_frameTimingWindow.WindowSeconds} s: waiting for samples";
        var frameTimingSummary = frameTiming.HasSamples
            ? $"Frame timing: rendered={renderFps:0.0} FPS; complete sent={frameTiming.CompleteFramesPerSecond:0.0}/s; " +
              $"avg complete-frame send={frameTiming.AverageSendDurationMilliseconds:0.0} ms; " +
              $"avg completion interval={frameTiming.AverageCompletionIntervalMilliseconds:0.0} ms"
            : $"Frame timing: rendered={renderFps:0.0} FPS; complete-frame timing waiting for samples";
        var serialErrors = Volatile.Read(ref _serialErrorTotal);
        var status =
            headline + Environment.NewLine +
            rollingSummary + Environment.NewLine +
            frameTimingSummary + Environment.NewLine +
            $"Transport: {SelectedBaudRate} 8N1, profile-validated optimized pacing; " +
            $"scheduler={(SelectedProfile.FrameSchedulerInterval ?? TimeSpan.FromMilliseconds(50)).TotalMilliseconds:0} ms; " +
            $"serial errors={serialErrors}{Environment.NewLine}" +
            $"Audio pipeline: {visualizerName} ({seconds:0.000} s diagnostic window){Environment.NewLine}" +
            $"Capture callbacks: {callbackWindow} ({callbackWindow / seconds:0.0}/s), {callbackTotal} total{Environment.NewLine}" +
            $"PCM received: {byteWindow} bytes ({byteWindow / seconds:0} B/s), {byteTotal} total bytes; " +
            $"{sampleFrameWindow} stereo frames / {sampleFrameWindow * 2} channel samples " +
            $"({sampleFrameWindow / seconds:0} frames/s), {sampleFrameTotal} total frames at {sampleRate} Hz{Environment.NewLine}" +
            $"Raw RMS: L={leftRms:0.000000}, R={rightRms:0.000000}{Environment.NewLine}" +
            $"FFT windows: {fftWindow} ({fftWindow / seconds:0.0}/s), {fftTotal} total{Environment.NewLine}" +
            $"Real-time queues: audio={pendingAudio}, render={pendingRender}, serial={pendingSerial}; " +
            $"oldest queued audio={oldestPendingAge:0.0} ms{Environment.NewLine}" +
            latency + Environment.NewLine +
            $"Normalized bands: [{bands}]{Environment.NewLine}" +
            $"VoxelScene: {sceneStatistics.LitVoxelCount}/{scene.Width * scene.Depth * scene.Height} lit; " +
            $"max intensity={sceneStatistics.MaxIntensity}; max RGB=" +
            $"({sceneStatistics.MaxRed},{sceneStatistics.MaxGreen},{sceneStatistics.MaxBlue})";

        Trace.WriteLine($"[audio-pipeline] {status.Replace(Environment.NewLine, " | ")}");
        if (MirrorDiagnosticsToStandardOutput)
            Console.WriteLine($"[audio-pipeline] {status.Replace(Environment.NewLine, " | ")}");
        Dispatcher.UIThread.Post(() =>
        {
            EndToEndAudioAge = headline;
            AudioPipelineDiagnostic = status;
        });
    }

    private static string FormatPipelineLatency(FramePipelineTrace? frame)
    {
        if (frame is null || frame.Audio.SnapshotPublishedTimestamp == 0 ||
            frame.SecondWriteCompletedTimestamp == 0)
            return "End-to-end latency: waiting for a fully traced audio frame.";

        static double Ms(long start, long end) => PipelineClock.Elapsed(start, end).TotalMilliseconds;
        var audio = frame.Audio;
        return $"Stage latency (ms): source→capture={Ms(audio.AudioDataTimestamp, audio.CaptureReceivedTimestamp):0.0}; " +
               $"capture→analyzer={Ms(audio.CaptureReceivedTimestamp, audio.AnalyzerSubmittedTimestamp):0.0}; " +
               $"analyzer→FFT={Ms(audio.AnalyzerSubmittedTimestamp, audio.FftCompletedTimestamp):0.0}; " +
               $"FFT→snapshot={Ms(audio.FftCompletedTimestamp, audio.SnapshotPublishedTimestamp):0.0}; " +
               $"snapshot→render={Ms(audio.SnapshotPublishedTimestamp, frame.VisualizerStartedTimestamp):0.0}; " +
               $"render={Ms(frame.VisualizerStartedTimestamp, frame.SceneCompletedTimestamp):0.0}; " +
               $"scene→encode={Ms(frame.SceneCompletedTimestamp, frame.FrameEncodedTimestamp):0.0}; " +
               $"encode→write1={Ms(frame.FrameEncodedTimestamp, frame.FirstWriteStartedTimestamp):0.0}; " +
               $"serial write1→write2 done={Ms(frame.FirstWriteStartedTimestamp, frame.SecondWriteCompletedTimestamp):0.0}; " +
               $"total audio age={Ms(audio.AudioDataTimestamp, frame.SecondWriteCompletedTimestamp):0.0}";
    }

    private void LogRatesIfDue(bool force = false)
    {
        var elapsed = _rateClock.Elapsed;
        if ((!force && elapsed < TimeSpan.FromSeconds(1)) || elapsed <= TimeSpan.Zero) return;

        var encodedFrames = Interlocked.Exchange(ref _encodedFramesInWindow, 0);
        var sentFrames = Interlocked.Exchange(ref _sentFramesInWindow, 0);
        var bytesWritten = Interlocked.Exchange(ref _bytesWrittenInWindow, 0);
        _rateClock.Restart();
        if (force && encodedFrames == 0 && sentFrames == 0 && bytesWritten == 0) return;

        var seconds = elapsed.TotalSeconds;
        AppendDiagnostic($"rates ({seconds:0.000} s): {encodedFrames / seconds:0.0} encoded FPS; " +
            $"{sentFrames / seconds:0.0} sent FPS; {bytesWritten / seconds:0} B/s " +
            $"({bytesWritten / seconds / 1000:0.0} kB/s)");
    }

    private void AppendDiagnostic(string message)
    {
        var line = $"[{DateTimeOffset.Now:HH:mm:ss.fff}] {message}";
        Trace.WriteLine($"[hardware-path] {line}");
        if (!DiagnosticsVisible) return;
        Dispatcher.UIThread.Post(() =>
        {
            _diagnosticLogLines.Enqueue(line);
            while (_diagnosticLogLines.Count > MaximumDiagnosticLogLines)
                _diagnosticLogLines.Dequeue();
            DiagnosticLog = string.Join(Environment.NewLine, _diagnosticLogLines);
        });
    }

    private void PostStatus(string status, bool isAudio = false)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (isAudio) AudioStatus = status;
            else
            {
                DetailStatus = status;
                UserStatus = status;
            }
        });
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class FramePipelineTrace(AudioPipelineTiming audio)
    {
        public AudioPipelineTiming Audio { get; } = audio;
        public long VisualizerStartedTimestamp { get; set; }
        public long SceneCompletedTimestamp { get; set; }
        public long FrameEncodedTimestamp { get; set; }
        public long FirstWriteStartedTimestamp { get; set; }
        public long SecondWriteCompletedTimestamp { get; set; }
        public int SerialWriteCount { get; set; }
    }

    public async ValueTask DisposeAsync()
    {
        await SaveSettingsAsync().ConfigureAwait(false);
        await DisconnectAsync().ConfigureAwait(false);
        _device.FrameEncoded -= DeviceOnFrameEncoded;
        _device.FrameSent -= DeviceOnFrameSent;
        _transport.WriteCompleted -= TransportOnWriteCompleted;
        _transport.ErrorReceived -= TransportOnErrorReceived;
        _analyzer.SnapshotAvailable -= AnalyzerOnSnapshotAvailable;
        Builder.PreviewRequested -= BuilderOnPreviewRequested;
        Builder.PreviewStopped -= BuilderOnPreviewStopped;
        await _transport.DisposeAsync().ConfigureAwait(false);
    }

    private async Task SaveSettingsAsync()
    {
        var settings = new ApplicationSettings
        {
            SelectedProfileId = SelectedProfile.Id,
            SelectedSerialPath = SelectedSerialPort?.Path,
            BaudRate = SelectedBaudRate,
            SelectedAudioSourceId = SelectedAudioSource?.Id,
            SelectedVisualizerId = SelectedVisualizer.Id,
            Sensitivity = Sensitivity,
            Decay = Decay,
            DiagnosticsVisible = DiagnosticsVisible
        };
        try
        {
            await _settingsStore.SaveAsync(settings).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[settings] Could not save settings: {exception.Message}");
        }
    }
}
