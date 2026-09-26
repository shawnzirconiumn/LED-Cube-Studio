# Windows x64 packaging

`publish-windows.ps1` creates a conventional self-contained Windows x64 publish directory and a distributable ZIP. End users do not need to install .NET separately.

From the repository root in PowerShell:

```powershell
./packaging/windows/publish-windows.ps1
```

Outputs:

- `artifacts/windows-x64/`
- `artifacts/LED-Cube-Studio-win-x64.zip`

The ZIP contains the application, the GPL project license, third-party notices, and verified dependency license material. Publishing can be cross-compiled with a compatible .NET 8 SDK on Linux, but launching, audio capture, serial-device access, icon display, and rendering must be smoke-tested on Windows x64 before release.

The executable icon is `assets/branding/generated/led-cube-studio.ico`. It was generated from the original `assets/branding/led-cube-studio.svg` with 16, 24, 32, 48, 64, 128, and 256 pixel images; it has no relationship to any legacy icon.
