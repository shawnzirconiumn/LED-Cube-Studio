# Visualizer Builder schema

The current JSON schema version is **2**.

Version 2 adds `reactionStrength` to each control source and adds the `rainbow`
color mode with `cycleOffset` and `cycleSpeed`. Reaction strength is a single
gain applied after the builder's perceptual normalization of Music Loudness,
Frequency Band, and Beat. Constant and Automatic Pulse do not use it.

Files saved with schema version 1 are migrated in memory when opened. Existing
fixed and gradient colors, layer settings, and identifiers are preserved. The
new fields receive neutral defaults (`reactionStrength: 1`, zero color cycle
offset, and zero color cycle speed). The migrated definition is written as
version 2 the next time it is saved. Files from newer unsupported schema
versions remain rejected rather than being rewritten.
