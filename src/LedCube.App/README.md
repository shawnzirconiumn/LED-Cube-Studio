# LED Cube Studio

For development, from the repository root:

```bash
dotnet run --project src/LedCube.App/LedCube.App.csproj
```

To publish and install/update the self-contained Linux desktop application for
the current user:

```bash
./packaging/linux/install-user.sh
```

The launcher is installed in the desktop application menu and the application
files are installed below `~/.local/opt/led-cube-studio`. See
`packaging/linux/README.md` for publishing and uninstall details.

On Linux, install `pactl` and `parec` if they are not already present and ensure your user can open the selected serial device (typically by membership in the distribution's serial-port group). The current `pactl get-default-sink` monitor is selected automatically unless a still-available saved source was explicitly selected. Stable `/dev/serial/by-id/...` entries are preferred and duplicate kernel paths such as `/dev/ttyUSB0` are hidden when they resolve to the same device.

Select the implemented **8x8x8 RGB** profile, an output-monitor audio source, the cube serial port, and one of the two Phase 1 visualizers, then connect.

The visualizer, sensitivity, and decay controls apply live while connected. Device profile, serial, stream mode, and audio-source controls are locked until disconnect so the UI cannot imply that an inactive configuration change took effect. Use the audio-source **Refresh** button after disconnecting to re-enumerate devices.

Basic selections and response settings are stored as JSON in the platform application-data directory (`~/.config/LedCubeStudio/settings.json` on typical Linux installations). Missing, stale, malformed, and out-of-range settings fall back safely.

## Development diagnostics

Choose **Hardware test: solid red** (or green/blue) in the stream-mode selector before connecting. In this mode the app does not create an audio capture or run the FFT/visualizer. It continuously encodes and sends the same full-cube color at the profile's validated cadence (approximately 18.2 FPS for the 8x8x8 RGB cube).

Expand **Diagnostics** to see the temporary hardware and audio-pipeline telemetry. Normal startup keeps this section collapsed. Its visibility is remembered between sessions.

The telemetry reports every encoded frame size, every completed serial write and its interval from the previous write, plus one-second rates for encoded frames, fully sent frames, and written bytes. For the 8x8x8 RGB profile, a healthy path should show 394-byte frames (two 197-byte writes), approximately 20 encoded/s and sent/s, and approximately 7,880 B/s (7.9 kB/s).

When **Audio visualizer** is selected, a separate pipeline panel updates about twice per second. It reports capture callbacks, PCM bytes and stereo sample frames, raw left/right RMS, completed overlapping FFT windows, all eight normalized bands, and the rendered scene's lit-voxel and maximum color/intensity values. This panel is intentionally observational: it does not alter capture, FFT, visualization, serial, or encoding behavior.
