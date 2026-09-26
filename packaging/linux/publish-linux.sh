#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
repo_root="$(cd -- "$script_dir/../.." && pwd -P)"
project="$repo_root/src/LedCube.App/LedCube.App.csproj"
output_dir="$repo_root/artifacts/linux-x64"
publish_stamp="$repo_root/artifacts/linux-x64.publish-stamp"

rm -f -- "$publish_stamp"
rm -rf -- "$output_dir"
mkdir -p -- "$output_dir"

dotnet publish "$project" \
  -m:1 \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --output "$output_dir" \
  -p:PublishSingleFile=false

test -x "$output_dir/led-cube-studio"
touch -- "$publish_stamp"
printf 'Published LED Cube Studio to %s\n' "$output_dir"
