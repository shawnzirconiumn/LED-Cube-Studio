# LED Cube Studio architecture

LED Cube Studio is a standalone .NET 8 application. It does not reference or include the inherited desktop application or firmware.

## Runtime flow

`IAudioCapture` → normalized stereo `AudioBuffer` → `SpectrumAnalyzer` → `AudioAnalysisSnapshot` → `IVisualizer` → `VoxelScene` → `CubeDeviceProfile` + `IFrameEncoder` → `EncodedFrame` → `ISerialTransport`

## Classes and responsibilities

| Area | Main types | Responsibility |
|---|---|---|
| Logical scene | `VoxelScene`, `VoxelColor` | Arbitrary X/Y/Z storage, normalized coordinate helper, RGB plus explicit monochrome intensity. |
| Audio analysis | `AudioBuffer`, `SpectrumAnalyzer`, `AudioAnalysisSnapshot` | Shared PCM processing: 2048-sample FFT, 1024-sample hop, Hann window, logarithmic bands, dBFS normalization, fixed fast attack, elapsed-time/log-half-life release smoothing, and adaptive bass beat envelope. |
| Capture | `IAudioCapture`, `PlatformAudioCaptureFactory` | Replaceable platform boundary. WASAPI loopback is used on Windows; `pactl` discovery selects the current default sink monitor and `parec` provides float PCM on Linux with an explicit 20 ms latency / 10 ms process-time request. |
| Visualization | `IVisualizer`, `EightBandSpectrumVisualizer`, `BassMidTrebleSpaceVisualizer` | Draw only into the logical scene. Geometry derives from scene dimensions; no serial or packet access. |
| Device profile | `CubeDeviceProfile`, `DeviceProfiles`, `AxisOrientation` | Dimensions, RGB/mono capability, validated baud policy and packet pacing, orientation, stable identifier hint, encoder selection, and protocol support status. |
| Encoding | `IFrameEncoder`, `Rgb8x8x8FrameEncoder` | Converts a logical scene to device-specific packets. Device-specific dimensions, framing, and wiring offsets are confined to the encoder. |
| Transport | `ISerialTransport`, `SerialPortTransport`, `SerialPortDiscovery` | Cross-platform 8N1 byte transport; stable Linux by-id paths are preferred and deduplicated against `ttyUSB`/`ttyACM`, while Windows uses COM discovery. |
| Orchestration/UI | `CubeDevice`, `MainWindowViewModel`, Avalonia views, `JsonApplicationSettingsStore` | Connection lifecycle, one profile-paced render loop, live visualizer/response selection, user-facing status, diagnostics visibility, and resilient platform-local settings. |

## Implementation provenance

The FFT, audio analysis, and visualizers were developed for this project from user-directed concepts with AI-assisted implementation. No third-party source code was knowingly copied or adapted. The production `Rgb8x8x8FrameEncoder` was implemented from the independently measured protocol recorded in `rgb8x8x8-protocol-observations.md`; the earlier legacy-derived encoder is not part of this repository.

## Fixed assumptions and where they belong

The audio band count of `8` belongs to the Phase 1 analysis contract, not cube geometry. A visualizer maps those bands to whatever scene width is selected.

The current device-specific encoder contains the measured 8×8×8 framing and mapping assumptions: physical size 8; 192 payload bytes per plane; 197 bytes per packet; 394 bytes per frame; the `00 FF 00 00` marker; plane values 0/1; and the observed green/blue/red byte ordering.

The current 8x8x8 RGB profile is physically validated only at 115200 baud, so its
normal UI exposes only that rate. Its default pacing starts packet two after the
197-byte 8N1 wire time plus a 1 ms receiver guard (approximately 18.1 ms between
packet starts). A profile-defined 55 ms frame cadence leaves approximately 22 ms
of receiver recovery time after the roughly 33 ms complete-frame send and yields
approximately 18.2 FPS. The earlier 50 ms cadence produced a rare physical artifact
during extended use despite clean host diagnostics. 460800 was
rejected after visible receiver corruption despite clean host diagnostics;
2,000,000 was intentionally not tested. These limits are profile-specific and do
not constrain future devices after their own firmware and transport validation.

## Future device encoders

The 8x8x32 RGB and 16x16x16 monochrome profiles intentionally have `EncoderId = null`. After each firmware is inspected:

1. Implement an `IFrameEncoder` with a unique ID in `LedCube.Hardware`.
2. Keep its dimensions, bit packing, headers, timing, and monochrome threshold/intensity behavior inside that class.
3. Register the encoder with `CubeDevice` in the app composition root.
4. Put that encoder ID and verified baud/orientation in the matching profile.

No UI or visualizer change is required. A monochrome encoder consumes `VoxelColor.Intensity`; RGB visualizers continue drawing colors normally.

## Phase 1 boundaries

- Linux audio requires `pactl` and `parec` (commonly supplied by `pulseaudio-utils`); it works against native PulseAudio or PipeWire-Pulse.
- Windows audio uses NAudio's WASAPI loopback adapter. All FFT and visualization code is shared.
- Only the existing 8x8x8 RGB protocol is enabled. Selecting either placeholder profile explains that its firmware protocol is not yet implemented.
- Physical-cube validation still requires the hardware, an accessible serial device, and a live audio output. Automated tests validate FFT band placement, dimensional rendering, and the known packet bytes.
