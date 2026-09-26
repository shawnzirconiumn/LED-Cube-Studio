# Third-Party Notices

LED Cube Studio is licensed under GPL-3.0-or-later. The dependencies listed here are separate works and remain under their respective licenses.

## Verified direct package metadata

The project files and restored NuGet metadata identify these exact direct dependencies:

| Package | Version | Declared license |
| --- | ---: | --- |
| Avalonia | 11.2.6 | MIT |
| Avalonia.Desktop | 11.2.6 | MIT |
| Avalonia.Themes.Fluent | 11.2.6 | MIT |
| Avalonia.Fonts.Inter | 11.2.6 | MIT for the package; bundled Inter font material requires SIL OFL 1.1 treatment |
| NAudio | 2.2.1 | MIT |
| System.IO.Ports | 8.0.0 | MIT |
| Microsoft.NET.Test.Sdk | 17.12.0 | MIT (test/build only) |
| xunit | 2.9.2 | Apache-2.0 (test only) |
| xunit.runner.visualstudio | 2.8.2 | Apache-2.0 (test only) |

Exact license and notice files available from restored packages and their identified upstream revisions are preserved in `licenses/`. Their presence does not claim that every listed component ships in every runtime artifact.

## Audited Windows x64 artifact

The self-contained `win-x64` publish produced with .NET SDK 8.0.130 resolved and emitted these relevant components:

| Emitted component | Resolved version | Verified license material |
| --- | ---: | --- |
| Avalonia desktop assemblies | 11.2.6 | MIT license and Avalonia `NOTICE.md` from package repository commit `6a34161ac6ad502b0331a17cd23d194a9e895f12` |
| Avalonia.Fonts.Inter / embedded Inter fonts | 11.2.6 | Avalonia package metadata: MIT; font software: SIL OFL 1.1 with Inter Project Authors copyright |
| NAudio assemblies | 2.2.1 | MIT license supplied by the restored package |
| System.IO.Ports | 8.0.0 | MIT license and third-party notices supplied by the restored package |
| SkiaSharp and `libSkiaSharp.dll` | 2.88.9 | Package/native MIT license and exact Win32 third-party notices |
| HarfBuzzSharp and `libHarfBuzzSharp.dll` | 7.3.0.3 | Package/native MIT license and exact Win32 third-party notices |
| Avalonia ANGLE `av_libglesv2.dll` | 2.1.22045.20230930 | License supplied by the restored native package |
| MicroCom.Runtime | 0.11.0 | MIT; copyright 2021 Nikita Tsukanov |
| Tmds.DBus.Protocol | 0.20.0 | MIT license from package repository commit `db7af5a46d316dda9886663fd3a006f6199343e1` |
| System.IO.Pipelines | 8.0.0 | MIT license and third-party notices supplied by the restored package |
| Microsoft .NET runtime for win-x64 | 8.0.30 | Runtime-pack license and third-party notices supplied by `Microsoft.NETCore.App.Runtime.win-x64` |

The Windows publishing script copies `LICENSE`, this notice, and the complete `licenses/` directory into both the publish directory and ZIP.

## Remaining artifact-specific work

Re-audit notices whenever package versions, the .NET runtime patch, publish settings, or emitted files change. The Linux artifact still requires its own finalized runtime-specific notice audit; Windows execution and rendering must be smoke-tested on a real Windows x64 system before release.

No dependency or font listed above is relicensed under the project's GPL license.
