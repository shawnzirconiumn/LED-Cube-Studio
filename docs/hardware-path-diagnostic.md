# Temporary hardware-path diagnostic

The Avalonia app's stream-mode selector includes solid red, green, and blue hardware tests. These modes fill one `VoxelScene` once and repeatedly send it without starting audio capture, processing PCM, running the FFT, or invoking a visualizer. They intentionally use the existing profile baud rate, orientation, encoder mapping, and validated transport pacing.

## Expected telemetry

- One encoded frame is 394 bytes: two 197-byte packets.
- At the validated 18.2 FPS cadence, payload throughput is approximately 7,171 B/s (7.2 kB/s).
- Each frame produces two serial-write entries. Their timestamps expose the safe packet-start interval and the interval to the next frame.
- Encoded FPS can exceed sent FPS temporarily if a write stalls or fails. A frame is counted as sent only after both packet writes complete.

## Avalonia versus legacy WinForms send loops

| Behavior | Avalonia app | Legacy WinForms app |
|---|---|---|
| Complete RGB frame | Two 197-byte writes | Two 197-byte writes |
| Packet marker/depth and voxel mapping | Same legacy layout | Original layout |
| Delay inside a frame | Packet starts separated by wire time plus a validated 1 ms guard (~18.1 ms at 115200) | Blocking 24 ms sleep after packet 0 |
| Delay after packet 1 | None in `CubeDevice.SendAsync` | Blocking 24 ms sleep before `SerialHelper.SendPacket` returns |
| Audio render cadence | Profile-defined `PeriodicTimer`, validated at 55 ms for this cube | Spectrum path gates renders with a 50 ms stopwatch; other modes are activity/speed driven |
| Solid-color test | Repeats continuously while connected | Settings color test sends three frames per selection |
| Write API | Serialized `BaseStream.WriteAsync` followed by `FlushAsync` | Synchronous `SerialPort.Write` |
| Observability | Separate encoded/s, fully sent/s, B/s, frame size, and write-gap logs | No equivalent transport counters |

The Avalonia timer holds packet-one frame starts to a 55 ms cadence. There is no blocking sleep after packet two, but the approximately 33 ms send leaves about 22 ms of receiver recovery time before the next frame. If serial writes take longer than the period, ticks coalesce and the measured sent rate drops, which is visible in the diagnostic counters.

## Validated 8x8x8 RGB transport

- Baud: 115200 8N1, enforced by the current device profile.
- Frame: two ordered 197-byte packets, 394 bytes total, unchanged from the legacy encoder.
- Pacing: packet wire time plus a 1 ms receiver guard between packet starts; 55 ms frame scheduler interval.
- Current scheduler ceiling: approximately 18.2 complete frames/s.
- Measured complete-frame send duration: approximately 33 ms.

The earlier 50 ms B0 cadence produced a rare top-layer horizontal red line during
extended use despite passing shorter tests. B5's 55 ms cadence was clean in both a
focused 3.3-minute run and a several-hour physical run. B10 was intentionally not
tested because B5 passed. Host queues and serial-error counters remained clean in
both cases, reinforcing that physical receiver output is authoritative.

460800 is rejected for this profile. Although host queues and error counters remained
clean, the physical cube showed horizontal colored lines, missing 8-band patches,
and output at zero sensitivity. 2,000,000 was intentionally skipped after that
failure. This protocol is transmit-only: host-side success means bytes were handed
to the serial transport, not that the receiver parsed them correctly. Any future
baud or pacing change therefore requires physical cube validation.

## Temporary end-to-end audio latency diagnostic

Audio mode also carries one monotonic timestamp trace from the center of each FFT window through capture, analyzer submission, FFT completion, snapshot publication, rendering, scene completion, encoding, and both serial writes. The diagnostics expander updates approximately twice per second with:

- every stage-to-stage duration and total audio age at completion of packet two;
- current audio, render, and serial pending counts plus the oldest pending-audio age;
- actual render FPS alongside the existing encoded/sent/write rates;
- the existing PCM, RMS, FFT, normalized-band, and scene-activity counters.

There are no application FIFO queues in this path: capture calls the analyzer synchronously, the analyzer replaces a single `Latest` snapshot, and the render loop sends one frame at a time. `PeriodicTimer` coalesces missed ticks instead of enqueuing render work.

The Linux delay was upstream of those stages. Without an explicit request, PipeWire-Pulse negotiated a 768,000-byte `parec` recording fragment—2,000 ms at 48 kHz stereo float32—and measured capture latency stabilized around 1,840–1,995 ms. The app now requests 20 ms capture latency and 10 ms process time. On the same host this negotiated a 7,680-byte fragment and measured approximately 0–19 ms capture latency. FFT normalization, serial timing, packet format, baud rate, and firmware were not changed.
