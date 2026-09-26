# Physical visualizer curation

The normal selector is curated for effects that remain active and visibly
three-dimensional on the physical 8x8x8 RGB cube at its validated 55 ms render
cadence. The following implementations remain available for geometry reuse but
are no longer registered:

- Tri-Band Wave Space
- Tri-Band Rotating Space
- Triple Orbit Shells
- Reactive Helix
- Audio Tornado

## Added active modes

- RGB Supernova
- Beat Fireworks
- Spectral Crossfire
- Frequency Storm
- Beat Comets
- RGB Floorquake
- Spectral Implosion / Explosion
- Audio Fire
- Beat Cubes
- Spectrum Waves 3D
- Spectral Particle Burst
- RGB Pulse Grid

These modes use elapsed-time motion. Fireworks, comets, particle bursts, pulse
grids, cubes, and wave generations use fixed-capacity arrays rather than growing
collections. All geometry continues to derive from `VoxelScene` dimensions and
adapts RGB output for monochrome profiles.

Bass Shockwave now retains six recent waves. Each beat launches a shell that
crosses the cube in approximately 320–480 ms, with the dominant spectrum band
and successive generations varying its color. Multiple shells may overlap, and
high-frequency energy adds blue/violet/white detail.

The shared visualizer output guard still guarantees a black scene at 0%
sensitivity without clearing FFT or visualizer state. Automated coverage renders
every registered mode on 8x8x8 RGB, 8x8x32 RGB, and 16x16x16 monochrome scenes,
checks unique IDs, verifies bounded state, and confirms visible response at 25%,
50%, and 100% for the new modes.
