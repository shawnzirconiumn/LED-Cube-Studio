#!/usr/bin/env bash
set -euo pipefail

data_home="${XDG_DATA_HOME:-$HOME/.local/share}"
install_dir="$HOME/.local/opt/led-cube-studio"
applications_dir="$data_home/applications"
desktop_file="$applications_dir/led-cube-studio.desktop"
icon_sizes=(16 32 48 64 128 256 512)

rm -rf -- "$install_dir"
rm -f -- "$desktop_file"
for size in "${icon_sizes[@]}"; do
  rm -f -- "$data_home/icons/hicolor/${size}x${size}/apps/led-cube-studio.png"
done
rm -f -- "$data_home/icons/hicolor/scalable/apps/led-cube-studio.svg"

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$applications_dir"
fi

printf 'Uninstalled LED Cube Studio. User settings were preserved.\n'
