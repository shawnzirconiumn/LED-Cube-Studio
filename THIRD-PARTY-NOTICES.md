# Third-Party Notices

LED Cube Studio's independently authored project code and original branding are licensed under **GPL-3.0-or-later**. See `LICENSE`. The dependencies below are separate works and remain under their respective licenses; they are not relicensed under the project's GPL license.

This inventory is based on the package versions and runtime assets recorded in the published application's `led-cube-studio.deps.json`, checked against the files actually emitted by the self-contained `linux-x64` and `win-x64` publishes. Exact legal texts are stored under `licenses/` rather than repeated here.

## Shared distributed dependencies

These managed components are present in both audited self-contained artifacts. Some packages also supply platform-specific native files described in the later sections.

| Distributed component | Version | License/notice material |
| --- | ---: | --- |
| Avalonia, Avalonia.Desktop, Avalonia.FreeDesktop, Avalonia.Native, Avalonia.Remote.Protocol, Avalonia.Skia, Avalonia.Themes.Fluent, Avalonia.Win32, and Avalonia.X11 | 11.2.6 | MIT: `Avalonia-11.2.6-MIT.txt`; upstream notices: `Avalonia-11.2.6-NOTICE.md` |
| Avalonia.Fonts.Inter | 11.2.6 | Avalonia package code is MIT as above; embedded Inter font software is SIL OFL 1.1: `Inter-OFL-1.1.txt` |
| NAudio, NAudio.Asio, NAudio.Core, NAudio.Midi, NAudio.Wasapi, and NAudio.WinMM | 2.2.1 | MIT: `NAudio-2.2.1.txt` |
| System.IO.Ports | 8.0.0 | MIT: `System.IO.Ports-8.0.0.txt`; notices: `System.IO.Ports-8.0.0-THIRD-PARTY-NOTICES.txt` |
| SkiaSharp | 2.88.9 | MIT: `SkiaSharp-2.88.9.txt`; native notices are platform-specific below |
| HarfBuzzSharp | 7.3.0.3 | MIT: `HarfBuzzSharp-7.3.0.3.txt`; native notices are platform-specific below |
| MicroCom.Runtime | 0.11.0 | MIT: `MicroCom.Runtime-0.11.0.txt` |
| Tmds.DBus.Protocol | 0.20.0 | MIT: `Tmds.DBus.Protocol-0.20.0.txt` |
| System.IO.Pipelines | 8.0.0 | MIT: `System.IO.Pipelines-8.0.0-LICENSE.txt`; notices: `System.IO.Pipelines-8.0.0-THIRD-PARTY-NOTICES.txt` |

`Microsoft.NET.Test.Sdk` 17.12.0, `xunit` 2.9.2, and `xunit.runner.visualstudio` 2.8.2 are test/build dependencies and are not present in either application publish artifact.

## Linux x64 distributed components

The audited self-contained Linux artifact contains 249 files: 228 application/runtime payload files plus 21 project and third-party licensing files. It was produced by .NET SDK 8.0.130. In addition to the shared managed components above, it distributes:

| Distributed component/file | Version | License/notice material |
| --- | ---: | --- |
| Microsoft .NET runtime and Linux x64 runtime files | 8.0.30 | `dotnet-runtime-linux-x64-8.0.30-LICENSE.txt`; `dotnet-runtime-linux-x64-8.0.30-THIRD-PARTY-NOTICES.txt` |
| SkiaSharp native `libSkiaSharp.so` | 2.88.9 | Shared MIT license above; `SkiaSharp.NativeAssets.Linux-2.88.9-THIRD-PARTY-NOTICES.txt` |
| HarfBuzzSharp native `libHarfBuzzSharp.so` | 7.3.0.3 | Shared MIT license above; `HarfBuzzSharp.NativeAssets.Linux-7.3.0.3-THIRD-PARTY-NOTICES.txt` |
| System.IO.Ports native `libSystem.IO.Ports.Native.so` | 8.0.0 | `System.IO.Ports.Native.Linux-x64-8.0.0-LICENSE.txt`; `System.IO.Ports.Native.Linux-x64-8.0.0-THIRD-PARTY-NOTICES.txt` |

The emitted ELF files dynamically require host libraries including glibc, libstdc++, libgcc, fontconfig, zlib, OpenSSL, and optionally LTTng. Those host libraries are not copied into the application artifact. Their files and licenses therefore remain the responsibility of the user's Linux distribution and are not represented as bundled components here.

No Windows ANGLE or Win32 Skia/HarfBuzz native binary is present in the Linux artifact. Linux packaging intentionally includes only shared and Linux-specific license material.

## Windows x64 distributed components

The audited self-contained Windows artifact was produced by .NET SDK 8.0.130. In addition to the shared managed components above, it distributes:

| Distributed component/file | Version | License/notice material |
| --- | ---: | --- |
| Microsoft .NET runtime and Windows x64 runtime files | 8.0.30 | `dotnet-runtime-win-x64-8.0.30-LICENSE.txt`; `dotnet-runtime-win-x64-8.0.30-THIRD-PARTY-NOTICES.txt` |
| SkiaSharp native `libSkiaSharp.dll` | 2.88.9 | Shared MIT license above; `SkiaSharp.NativeAssets.Win32-2.88.9-THIRD-PARTY-NOTICES.txt` |
| HarfBuzzSharp native `libHarfBuzzSharp.dll` | 7.3.0.3 | Shared MIT license above; `HarfBuzzSharp.NativeAssets.Win32-7.3.0.3-THIRD-PARTY-NOTICES.txt` |
| Avalonia ANGLE `av_libglesv2.dll` | 2.1.22045.20230930 | `Avalonia.ANGLE.Windows-2.1.22045.20230930.txt` |

The Windows publishing script currently copies the complete repository `licenses/` directory, so it may contain clearly named Linux-specific texts in addition to the material required by Windows. This does not indicate that Linux native binaries are distributed in the Windows artifact.

## Audit maintenance

Re-audit both platform inventories whenever package versions, the .NET runtime patch, publish settings, or emitted files change. Platform execution testing is separate from this file-level dependency and notice audit.
