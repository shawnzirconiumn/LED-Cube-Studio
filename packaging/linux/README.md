# Linux packaging

These scripts produce a conventional, self-contained `linux-x64` publish tree
that can be installed for the current user today and archived as a GitHub
release artifact later.

- `./packaging/linux/publish-linux.sh` publishes to `artifacts/linux-x64/`.
- `./packaging/linux/install-user.sh` publishes and installs the app below
  `~/.local/opt/led-cube-studio/`, with desktop and icon integration in the
  user's XDG data directory.
- `./packaging/linux/uninstall-user.sh` removes those installed application
  files. It intentionally preserves settings such as
  `~/.config/LedCubeStudio/settings.json`.

The publish directory includes `LICENSE.txt`, `THIRD-PARTY-NOTICES.md`, and
the exact shared and Linux-specific files selected from `licenses/`. Windows-only
native notices, including ANGLE, are intentionally not copied into the Linux
artifact.

The original LED Cube Studio icon is maintained in
`assets/branding/led-cube-studio.svg`, which is the canonical source asset.
The installer registers that SVG as a scalable icon and installs generated PNG
exports at 16, 32, 48, 64, 128, 256, and 512 pixels. Raster exports live under
`assets/branding/generated/`; the application embeds the 256-pixel export.
