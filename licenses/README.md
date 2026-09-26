# License material provenance

This directory preserves verbatim license and notice texts from restored NuGet packages or identified upstream repositories. The tables below record the source used so the material can be rechecked when versions change.

## Shared dependency files

| File | Provenance |
| --- | --- |
| `Avalonia-11.2.6-MIT.txt` | `licence.md` from Avalonia repository commit `6a34161ac6ad502b0331a17cd23d194a9e895f12`, the commit recorded by the 11.2.6 NuGet packages. |
| `Avalonia-11.2.6-NOTICE.md` | `NOTICE.md` from the same Avalonia commit. |
| `Inter-OFL-1.1.txt` | Official Inter `LICENSE.txt`. Avalonia commit `3c974b09c72c74caed74dfc067a5913b00ba873e` introduced the exact font blobs retained at the 11.2.6 package commit; its pull request records that they were downloaded from Google Fonts. The package does not carry a separate OFL text. |
| `NAudio-2.2.1.txt` | `license.txt` from the restored NAudio 2.2.1 package. |
| `SkiaSharp-2.88.9.txt` | `LICENSE.txt` from the restored SkiaSharp 2.88.9 package. The native-package license files are byte-identical. |
| `HarfBuzzSharp-7.3.0.3.txt` | `LICENSE.txt` from the restored HarfBuzzSharp 7.3.0.3 package. The native-package license files are byte-identical. |
| `System.IO.Ports-8.0.0.txt` | `LICENSE.TXT` from the restored System.IO.Ports 8.0.0 managed package. |
| `System.IO.Ports-8.0.0-THIRD-PARTY-NOTICES.txt` | `THIRD-PARTY-NOTICES.TXT` from that package. |
| `System.IO.Pipelines-8.0.0-LICENSE.txt` | `LICENSE.TXT` from the restored System.IO.Pipelines 8.0.0 package. |
| `System.IO.Pipelines-8.0.0-THIRD-PARTY-NOTICES.txt` | `THIRD-PARTY-NOTICES.TXT` from that package. |
| `Tmds.DBus.Protocol-0.20.0.txt` | `COPYING` from Tmds.DBus repository commit `db7af5a46d316dda9886663fd3a006f6199343e1`, the commit recorded by the 0.20.0 package. |
| `MicroCom.Runtime-0.11.0.txt` | Upstream MicroCom `LICENSE`; its copyright matches the 0.11.0 package metadata. The package declares MIT but does not identify a source commit or include a license file, so the upstream text cannot be pinned more narrowly to the package build. |

## Linux-specific files

| File | Provenance |
| --- | --- |
| `dotnet-runtime-linux-x64-8.0.30-LICENSE.txt` | `LICENSE.TXT` from restored `Microsoft.NETCore.App.Runtime.linux-x64` 8.0.30. |
| `dotnet-runtime-linux-x64-8.0.30-THIRD-PARTY-NOTICES.txt` | `THIRD-PARTY-NOTICES.TXT` from that runtime pack. |
| `SkiaSharp.NativeAssets.Linux-2.88.9-THIRD-PARTY-NOTICES.txt` | `THIRD-PARTY-NOTICES.txt` from restored `SkiaSharp.NativeAssets.Linux` 2.88.9. |
| `HarfBuzzSharp.NativeAssets.Linux-7.3.0.3-THIRD-PARTY-NOTICES.txt` | `THIRD-PARTY-NOTICES.txt` from restored `HarfBuzzSharp.NativeAssets.Linux` 7.3.0.3. |
| `System.IO.Ports.Native.Linux-x64-8.0.0-LICENSE.txt` | `LICENSE.TXT` from restored `runtime.linux-x64.runtime.native.System.IO.Ports` 8.0.0. This differs from the managed package license file and is retained separately. |
| `System.IO.Ports.Native.Linux-x64-8.0.0-THIRD-PARTY-NOTICES.txt` | `THIRD-PARTY-NOTICES.TXT` from that native runtime package. |

## Windows-specific files

| File | Provenance |
| --- | --- |
| `dotnet-runtime-win-x64-8.0.30-LICENSE.txt` | `LICENSE.TXT` from restored `Microsoft.NETCore.App.Runtime.win-x64` 8.0.30. |
| `dotnet-runtime-win-x64-8.0.30-THIRD-PARTY-NOTICES.txt` | `THIRD-PARTY-NOTICES.TXT` from that runtime pack. |
| `SkiaSharp.NativeAssets.Win32-2.88.9-THIRD-PARTY-NOTICES.txt` | `THIRD-PARTY-NOTICES.txt` from restored `SkiaSharp.NativeAssets.Win32` 2.88.9. |
| `HarfBuzzSharp.NativeAssets.Win32-7.3.0.3-THIRD-PARTY-NOTICES.txt` | `THIRD-PARTY-NOTICES.txt` from restored `HarfBuzzSharp.NativeAssets.Win32` 7.3.0.3. |
| `Avalonia.ANGLE.Windows-2.1.22045.20230930.txt` | `LICENSE` from restored `Avalonia.Angle.Windows.Natives` 2.1.22045.20230930. |

The only provenance limitation found is the unpinned MicroCom upstream license noted above; its exact NuGet metadata still verifies the MIT expression and copyright. No license file in this directory has unknown package/version association.
