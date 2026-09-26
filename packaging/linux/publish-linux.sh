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

cp -- "$repo_root/LICENSE" "$output_dir/LICENSE.txt"
cp -- "$repo_root/THIRD-PARTY-NOTICES.md" "$output_dir/THIRD-PARTY-NOTICES.md"
mkdir -p -- "$output_dir/licenses"

license_files=(
  Avalonia-11.2.6-MIT.txt
  Avalonia-11.2.6-NOTICE.md
  HarfBuzzSharp-7.3.0.3.txt
  HarfBuzzSharp.NativeAssets.Linux-7.3.0.3-THIRD-PARTY-NOTICES.txt
  Inter-OFL-1.1.txt
  MicroCom.Runtime-0.11.0.txt
  NAudio-2.2.1.txt
  README.md
  SkiaSharp-2.88.9.txt
  SkiaSharp.NativeAssets.Linux-2.88.9-THIRD-PARTY-NOTICES.txt
  System.IO.Pipelines-8.0.0-LICENSE.txt
  System.IO.Pipelines-8.0.0-THIRD-PARTY-NOTICES.txt
  System.IO.Ports-8.0.0.txt
  System.IO.Ports-8.0.0-THIRD-PARTY-NOTICES.txt
  System.IO.Ports.Native.Linux-x64-8.0.0-LICENSE.txt
  System.IO.Ports.Native.Linux-x64-8.0.0-THIRD-PARTY-NOTICES.txt
  Tmds.DBus.Protocol-0.20.0.txt
  dotnet-runtime-linux-x64-8.0.30-LICENSE.txt
  dotnet-runtime-linux-x64-8.0.30-THIRD-PARTY-NOTICES.txt
)

for license_file in "${license_files[@]}"; do
  cp -- "$repo_root/licenses/$license_file" "$output_dir/licenses/$license_file"
done

touch -- "$publish_stamp"
printf 'Published LED Cube Studio to %s\n' "$output_dir"
