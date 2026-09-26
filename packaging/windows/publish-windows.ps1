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
Copy-Item -Recurse (Join-Path $RepoRoot "licenses") $OutputDirectory

Compress-Archive -Path (Join-Path $OutputDirectory "*") -DestinationPath $ArchivePath -CompressionLevel Optimal

Write-Host "Published LED Cube Studio to $OutputDirectory"
Write-Host "Created release archive $ArchivePath"
