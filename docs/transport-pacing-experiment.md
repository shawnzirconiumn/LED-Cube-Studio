# Transport pacing experiment

Temporary host presets were tested without changing the 394-byte logical frame,
packet headers, encoder mapping, FFT, visualizers, or saved application defaults.
Each frame remained two 197-byte packets.

## Physical results

| Preset | Host / firmware | Audio age min / avg / max | Render / sent | Send duration | Frame interval | Queues / errors | Physical result |
| --- | --- | ---: | ---: | ---: | ---: | --- | --- |
| A | 115200, legacy 24 ms pacing | 139.7 / 161.4 / 182.4 ms | 17.7 / 17.7 FPS | 56.2 ms | 56.5 ms | 0 / 0 | Stable baseline |
| B0 | 115200, wire time + 1 ms | 145.7 / 163.4 / 184.0 ms | 20.0 / 20.0 FPS | 33.0 ms | 50.0 ms | 0 / 0 | Rejected after extended use: rare top-layer horizontal red line, approximately once per minute |
| B5 | 115200, wire time + 1 ms | 139.2 / 161.5 / 182.6 ms | 18.2 / 18.2 FPS | 33.0 ms | 55.0 ms | 0 / 0 | Clean focused 3.3-minute run and clean several-hour run; no observed corruption |
| B10 | 115200, wire time + 1 ms | Not run | Not run | Not run | Not run | Not run | Intentionally skipped because B5 was clean |
| C | 460800, wire time + 1 ms | 86.4 / 108.2 / 125.8 ms | 20.0 / 20.0 FPS | 9.6 ms | 50.0 ms | 0 / 0 | Failed: sporadic colored horizontal lines, missing 8-band patches, and output at zero sensitivity |
| D | 2000000, wire time + 1 ms | Not run | Not run | Not run | Not run | Not run | Skipped because C already failed physical integrity |

The latency values are rolling diagnostic samples, so changes in Linux capture age
and audio timing can move the end-to-end values between runs. The transport-specific
comparison is clearer: B5 retains the approximately 23 ms send-duration reduction
while its conservative 55 ms scheduler cadence provides about 22 ms of receiver
recovery time after packet two.

Host-side queue and serial-error counters did not detect C's corruption. Physical
output validation is therefore required before treating a higher baud preset as
reliable on this MCU/CH340/cube path.

## Recommendation

Preset B5 was selected as the normal default: it remained physically clean for
several hours, improves frame cadence and send duration over A, and retains the
known-compatible 115200 baud rate. B0 is not considered stable because a rare
receiver artifact appeared only during longer operation. B10 should not be tested
unless corruption returns. Preset C must not be used as a default, and preset D
should remain untested unless the higher-rate firmware path is first made reliable.

The temporary preset selector and experiment startup hooks were removed after the
test. The validated 8x8x8 profile now applies B5's optimized packet pacing and
55 ms scheduler interval directly and exposes only 115200 baud. The old encoder
delay remains an internal fallback, not a normal UI option. The cube was restored
to the known-good 115200 firmware, and BOOT0 was returned low after validation.
