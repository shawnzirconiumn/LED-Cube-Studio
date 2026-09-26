[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$Project = Join-Path $RepoRoot "src/LedCube.App/LedCube.App.csproj"
$ArtifactsDirectory = Join-Path $RepoRoot "artifacts"
$OutputDirectory = Join-Path $ArtifactsDirectory "windows-x64"
$ArchivePath = Join-Path $ArtifactsDirectory "LED-Cube-Studio-win-x64.zip"

if (Test-Path $OutputDirectory) {
    Remove-Item -Recurse -Force $OutputDirectory
}
if (Test-Path $ArchivePath) {
    Remove-Item -Force $ArchivePath
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

& dotnet publish $Project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $OutputDirectory `
    -p:PublishSingleFile=false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$Executable = Join-Path $OutputDirectory "led-cube-studio.exe"
if (-not (Test-Path $Executable)) {
    throw "Expected Windows executable was not produced: $Executable"
}

Copy-Item (Join-Path $RepoRoot "LICENSE") (Join-Path $OutputDirectory "LICENSE.txt")
Copy-Item (Join-Path $RepoRoot "THIRD-PARTY-NOTICES.md") $OutputDirectory

$LicensesDirectory = Join-Path $OutputDirectory "licenses"
New-Item -ItemType Directory -Force -Path $LicensesDirectory | Out-Null

$LicenseFiles = @(
    "Avalonia-11.2.6-MIT.txt"
    "Avalonia-11.2.6-NOTICE.md"
    "Avalonia.ANGLE.Windows-2.1.22045.20230930.txt"
    "HarfBuzzSharp-7.3.0.3.txt"
    "HarfBuzzSharp.NativeAssets.Win32-7.3.0.3-THIRD-PARTY-NOTICES.txt"
    "Inter-OFL-1.1.txt"
    "MicroCom.Runtime-0.11.0.txt"
    "NAudio-2.2.1.txt"
    "README.md"
    "SkiaSharp-2.88.9.txt"
    "SkiaSharp.NativeAssets.Win32-2.88.9-THIRD-PARTY-NOTICES.txt"
    "System.IO.Pipelines-8.0.0-LICENSE.txt"
    "System.IO.Pipelines-8.0.0-THIRD-PARTY-NOTICES.txt"
    "System.IO.Ports-8.0.0.txt"
    "System.IO.Ports-8.0.0-THIRD-PARTY-NOTICES.txt"
    "Tmds.DBus.Protocol-0.20.0.txt"
    "dotnet-runtime-win-x64-8.0.30-LICENSE.txt"
    "dotnet-runtime-win-x64-8.0.30-THIRD-PARTY-NOTICES.txt"
)

foreach ($LicenseFile in $LicenseFiles) {
    Copy-Item (Join-Path $RepoRoot "licenses/$LicenseFile") $LicensesDirectory
}

Compress-Archive -Path (Join-Path $OutputDirectory "*") -DestinationPath $ArchivePath -CompressionLevel Optimal

Write-Host "Published LED Cube Studio to $OutputDirectory"
Write-Host "Created release archive $ArchivePath"
