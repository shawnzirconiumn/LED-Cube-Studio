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

Exact license files available in the restored package cache for NAudio, System.IO.Ports, SkiaSharp 2.88.9, HarfBuzzSharp 7.3.0.3, and Avalonia's Windows ANGLE native package are preserved in `licenses/`. Their presence does not claim that every listed component ships in every runtime artifact.

## Release-artifact notice work still required

This is an initial source-repository inventory, not a final binary-distribution notice bundle. Before publishing Linux or Windows artifacts, generate and review notices against each final runtime-specific dependency graph and the files actually shipped. In particular:

- preserve the Avalonia package notices and copyrights for the exact resolved packages;
- include the complete SIL Open Font License 1.1 and copyright information for the exact Inter font files bundled by Avalonia;
- include matching SkiaSharp/Skia and HarfBuzzSharp/HarfBuzz licenses and upstream third-party notices;
- include the .NET runtime license and third-party notices for each self-contained runtime build;
- include ANGLE's exact license and native notices in Windows distributions when ANGLE is shipped; and
- check NAudio and all runtime-specific native or support packages against the finalized artifacts.

No dependency or font listed above is relicensed under the project's GPL license.
