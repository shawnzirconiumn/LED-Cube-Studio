# Decay control response

The Decay slider controls the release half-life rather than a coefficient per
FFT update. Its 0–100% range maps logarithmically from 1500 ms to 75 ms:

`halfLife = 1500 ms × (75 / 1500) ^ (slider / 100)`

For each FFT window, the analyzer measures elapsed audio time and derives the
release coefficient as:

`coefficient = 1 - exp(-ln(2) × elapsed / halfLife)`

This keeps release timing consistent if the FFT update rate changes. The fixed
0.65 attack coefficient is unchanged.

| Decay | Time from 1.0 to 0.5 | Time from 1.0 to 0.1 |
| ---: | ---: | ---: |
| 0% | 1500 ms | 4983 ms |
| 25% | 709 ms | 2356 ms |
| 50% | 335 ms | 1114 ms |
| 75% | 159 ms | 527 ms |
| 100% | 75 ms | 249 ms |

Lower values therefore remain slower, while each quarter of the slider has a
meaningful perceptual effect. Unit tests verify both these numerical targets and
equivalent one-second decay at 20, 47, and 100 analyzer updates per second.
