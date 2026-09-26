#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
repo_root="$(cd -- "$script_dir/../.." && pwd -P)"
publish_dir="$repo_root/artifacts/linux-x64"
publish_stamp="$repo_root/artifacts/linux-x64.publish-stamp"
branding_dir="$repo_root/assets/branding"

data_home="${XDG_DATA_HOME:-$HOME/.local/share}"
install_dir="$HOME/.local/opt/led-cube-studio"
applications_dir="$data_home/applications"
desktop_file="$applications_dir/led-cube-studio.desktop"
executable="$install_dir/led-cube-studio"
icon_sizes=(16 32 48 64 128 256 512)

newer_input=""
if [[ -f "$publish_stamp" ]]; then
  newer_input="$(find "$repo_root/src" "$branding_dir" \
    \( -path '*/bin' -o -path '*/obj' \) -prune -o \
    -type f -newer "$publish_stamp" -print -quit)"
fi

if [[ ! -x "$publish_dir/led-cube-studio" || ! -f "$publish_stamp" || -n "$newer_input" ]]; then
  "$script_dir/publish-linux.sh"
else
  printf 'Using existing Linux publish at %s\n' "$publish_dir"
fi

mkdir -p -- "$install_dir" "$applications_dir"
find "$install_dir" -mindepth 1 -maxdepth 1 -exec rm -rf -- {} +
cp -a -- "$publish_dir/." "$install_dir/"
chmod 0755 -- "$executable"

for size in "${icon_sizes[@]}"; do
  icons_dir="$data_home/icons/hicolor/${size}x${size}/apps"
  mkdir -p -- "$icons_dir"
  install -m 0644 -- \
    "$branding_dir/generated/led-cube-studio-${size}.png" \
    "$icons_dir/led-cube-studio.png"
done

scalable_icons_dir="$data_home/icons/hicolor/scalable/apps"
mkdir -p -- "$scalable_icons_dir"
install -m 0644 -- "$branding_dir/led-cube-studio.svg" \
  "$scalable_icons_dir/led-cube-studio.svg"

awk -v executable="$executable" \
  '{ sub(/@EXECUTABLE@/, executable); print }' \
  "$script_dir/led-cube-studio.desktop" > "$desktop_file"
chmod 0644 -- "$desktop_file"

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$applications_dir"
fi

printf 'Installed LED Cube Studio. Launch it from your desktop application menu.\n'
