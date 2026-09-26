# Release License and Provenance Audit

Original audit date: 2026-09-25  
Remediation update: 2026-09-26

This is an engineering provenance and release-readiness review, not legal advice.

## Executive verdict

**Verdict B — this clean source repository is suitable for pre-release development; artifact-specific third-party notice work remains before binary publication.**

The current repository should not be published wholesale under a new license. It is a clone of `Sliicy/8x8x8-LED`, contains that project's history and assets, and has no repository-level license granting redistribution or relicensing rights. In the absence of an explicit grant, the inherited material must be treated as all-rights-reserved.

A separately initialized **LED Cube Studio** repository can be released under the project owner's chosen **GPL-3.0-or-later** license for independently authored code after the remaining tasks below are completed and the release is assembled from an explicit allow-list. The reviewed application dependencies do not prevent that licensing choice, provided their own licenses and notices are preserved.

The clean repository, original icon, and **GPL-3.0-or-later** project license are now in place. Before publishing binaries, generate and verify exact-version notices and license material against the finalized Linux and Windows artifacts. Firmware and inherited legacy assets, code, and history remain outside v1.

The earlier FFT/visualizer provenance uncertainty and legacy-derived production encoder issue have been resolved as recorded below.

## Scope and method

This audit covered:

- the current working tree and Git history relative to `origin/master`;
- the legacy projects and assets at the repository root;
- the four current projects under `src/` and tests under `tests/`;
- firmware source and binary files;
- Linux packaging and the existing `artifacts/linux-x64/` publish output;
- direct and resolved NuGet dependencies and locally restored package metadata.

The configured origin is [`Sliicy/8x8x8-LED`](https://github.com/Sliicy/8x8x8-LED). At the time of the audit, local `HEAD` and `origin/master` both pointed to commit `3d3693a`; the cross-platform application, tests, packaging, and documentation were untracked additions relative to that commit. “Untracked” establishes when a file entered this checkout, but it does not by itself establish authorship or independence.

No `LICENSE`, `COPYING`, `NOTICE`, or equivalent grant was found in the inherited repository tree or history examined. Copyright therefore remains with the respective authors unless separate permission can be documented.

## Repository provenance inventory

| Area | Evidence and assessment | Release treatment |
| --- | --- | --- |
| `8x8x8-LED.Core/` | Tracked inherited C# implementation and supporting files. No license grant found. | Exclude from the new release repository. |
| `8x8x8-LED.WinForm/` | Tracked inherited WinForms application. Several files also have large local modifications. No license grant found. | Exclude. Do not use its code or artwork as distributable source material. |
| `Firmware/` | Tracked inherited source and binaries, plus local modifications and an untracked build option file. Mixed and incomplete provenance; see firmware section. | Exclude from v1. |
| `3D Printed Parts/` | Inherited design files with no license grant found. | Exclude. |
| `Examples/` | Inherited examples with no license grant found. | Exclude. |
| `Schematics/` | Inherited hardware design files with no license grant found. | Exclude. |
| `Screenshots/` | Inherited images with no license grant found. | Exclude. |
| Root `README.md`, legacy solution, and project metadata | Inherited documentation and build structure. | Replace with newly authored release documentation and solution metadata. |
| `src/LedCube.App/` | New Avalonia application structure with no matching inherited project. Mostly a candidate for release, subject to ownership confirmation and icon replacement. | Include after resolving listed exceptions. |
| `src/LedCube.Audio/` | New audio abstractions, PCM handling, Windows capture wrapper, and Linux `parec` implementation. No close inherited source counterpart identified. | Candidate for inclusion after author confirms ownership. |
| `src/LedCube.Core/` | New domain structure. The project owner confirms that the FFT/music-visualizer feature and presets were developed within this project from user-directed concepts and AI-assisted implementation, without knowingly copying or adapting third-party source code. | Candidate for inclusion as independently authored project code. |
| `src/LedCube.Hardware/` | New abstractions, discovery, profiles, transport, pacing, and diagnostics. The production RGB encoder has been independently reimplemented from measured hardware behavior. | Include the reviewed production source; exclude the legacy encoder from the clean repository. |
| `tests/LedCube.Tests/` | Newly created test suite. New RGB encoder tests were derived from the independently measured protocol specification rather than legacy expected-byte fixtures. | Include reviewed current tests; exclude old legacy encoder tests. |
| `packaging/linux/` | Newly authored scripts and desktop integration, except for the copied icon derivative. | Include scripts/metadata; replace icon. |
| `docs/` | Newly created project documentation, including the independent RGB protocol observation record and this updated provenance audit. | Include reviewed documentation in the clean repository. |

## Code-level findings

### Resolved FFT and visualizer provenance

The project owner confirms that the music-visualizer feature, FFT implementation, and visualizer presets were developed as part of this project from user-directed concepts and AI-assisted implementation. No third-party source code was knowingly copied or adapted for that implementation. Based on that project-owner attestation, the previous FFT/visualizer provenance uncertainty is resolved for release planning.

This records the provenance of the project code; it does not make or depend on any broader claim about copyright questions concerning AI training.

### Resolved production RGB encoder

The legacy-derived production encoder has been replaced by `src/LedCube.Hardware/Encoding/Rgb8x8x8FrameEncoder.cs`. The replacement was implemented from the independently measured physical-hardware specification in `docs/rgb8x8x8-protocol-observations.md`.

Physical probing independently established:

- packet framing;
- spatial mapping;
- RGB byte and channel ordering; and
- equal-weight additive packet planes.

The replacement uses an independently chosen three-level quantizer for each RGB channel:

```text
0-63    -> off
64-191  -> plane 0
192-255 -> planes 0 + 1
```

Production code now has zero dependency on `LegacyRgb8x8x8FrameEncoder`. The legacy encoder and its old tests remain only in the old working repository, are excluded from compilation, and will not be copied into the clean release repository.

After the replacement implementation and its independently derived tests were already complete, a reference-audit `rg` command accidentally printed only the legacy encoder type declaration and one old test field declaration. It displayed no implementation logic, mapping table, palette data, or expected-byte fixtures. This occurred after implementation and testing and did not supply input to the replacement encoder.

Physical regression testing on the actual cube succeeded: spatial mapping, RGB output, brightness response, and visualizer operation all appeared normal.

### Candidate independently authored areas

No close legacy counterpart or exact source copy was identified for these areas:

- Avalonia views, view models, settings, and application startup;
- audio capture interfaces, Linux PulseAudio process handling, Windows WASAPI integration, and PCM decoding;
- voxel scene and color types;
- device discovery abstractions, profiles, orientation handling, asynchronous serial transport, pacing, and diagnostics;
- Linux publishing/install scripts and desktop entry;
- most tests and current project documentation.

These are candidates for licensing by their actual author. Before release, the maintainer should confirm that all contributors and any assisted-generation inputs permit that licensing. This audit cannot infer ownership solely from filesystem state.

### Completed remediation for the encoder

The project completed the independent-specification option: it recorded the measured protocol, implemented a newly named production encoder without the legacy palette, created new specification-derived tests, switched production registration, and excluded the legacy encoder and old tests from compilation. The legacy files must remain outside the clean release repository.

## Asset audit

### Application icon

The release uses independently authored original artwork from `assets/branding/led-cube-studio.svg` and its generated PNG exports. The inherited `Cube.ico` and all legacy-derived icon files are excluded. No other application-specific image asset is included.

## Firmware audit

The firmware should not be included in the first LED Cube Studio release.

- The RGB Arduino source is inherited from the upstream repository and has no license header or repository-level license. Current local changes are small modifications on top of that source and do not create a separately licensable work.
- The firmware directories include precompiled `.hex` and other binary material described as original firmware. Source, author, and redistribution permission are not established for every binary.
- The monochrome source credits Tomas Uktveris (2015) and describes itself as derived from `tomazas` firmware, but the local repository does not carry a corresponding license grant.
- The present [`tomazas/ledcube8x8x8`](https://github.com/tomazas/ledcube8x8x8) repository contains an MIT license. That can help establish terms for material actually covered by that repository and version, but it does not retroactively license Sliicy's changes, unrelated RGB firmware, or opaque binaries in this checkout.

LED Cube Studio can ship as a host application for already-flashed compatible hardware. Its documentation may describe protocol compatibility and link to separately maintained firmware, but it should not imply ownership of or bundle firmware until each included file has a documented provenance and license.

## NuGet and bundled dependency audit

The following inventory is based on the project files, resolved assets, restored package metadata, and the current self-contained Linux publish output.

### Direct dependencies

| Project/package | Version | License evidence | Release consideration |
| --- | ---: | --- | --- |
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter | 11.2.6 | Package metadata uses MIT; the [Avalonia repository](https://github.com/AvaloniaUI/Avalonia) and [official FAQ](https://docs.avaloniaui.net/tools/faq) identify the framework as MIT. | Preserve MIT notices. Treat bundled Inter font files separately under OFL. |
| NAudio | 2.2.1 | [NAudio](https://github.com/naudio/NAudio) is MIT licensed; package includes its license. | Preserve MIT notice and Mark Heath copyright. |
| System.IO.Ports | 8.0.0 | Part of [dotnet/runtime](https://github.com/dotnet/runtime), MIT licensed. | Preserve .NET license and applicable third-party notices in self-contained distributions. |
| Microsoft.NET.Test.Sdk | 17.12.0 | [VSTest](https://github.com/microsoft/vstest) is MIT licensed. | Test/build-only; retain source-repository notices as appropriate. |
| xunit | 2.9.2 | [xUnit.net license](https://github.com/xunit/xunit/blob/main/LICENSE) is Apache-2.0. | Test-only; preserve license in source/dependency notices. |
| xunit.runner.visualstudio | 2.8.2 | Apache-2.0 package metadata. | Test-only; preserve license in source/dependency notices. |

### Important transitive and embedded components

| Component group | Observed license | Release consideration |
| --- | --- | --- |
| Avalonia platform packages, MicroCom, Tmds.DBus | MIT | Include their copyright/license notices. |
| SkiaSharp and native Skia | MIT package license, with additional upstream third-party notices | Include the [SkiaSharp license](https://github.com/mono/SkiaSharp/blob/main/LICENSE.md) and the third-party notices corresponding to the exact package/native binary. |
| HarfBuzzSharp and native HarfBuzz | MIT package license, with upstream third-party notices | Include package license and applicable native third-party notices. |
| Inter font files | SIL Open Font License 1.1 in the [Inter repository](https://github.com/rsms/inter) | Bundle the OFL text and copyright notice; do not represent the font itself as MIT. |
| Self-contained .NET runtime and native runtime packs | MIT plus third-party components | Follow [.NET's binary-distribution guidance](https://github.com/dotnet/runtime/blob/main/docs/project/licensing-assets.md) and include the matching .NET license and third-party notice files. |
| NAudio family and Microsoft support packages | MIT | Preserve included notices. |
| Windows ANGLE native package | BSD-style three-clause license in restored package | Relevant when producing Windows artifacts; include its license and native notices there. |
| Test transitives such as Microsoft.CodeCoverage and Newtonsoft.Json | MIT; not in the runtime publish | Record in the source dependency notice; do not need to ship in an application artifact that does not contain them. |

No dependency incompatible with the chosen GPL-3.0-or-later project license was identified in this audit. The dependencies retain their respective permissive or OFL terms, copyrights, and notice requirements; they should not be relabeled as GPL-covered project code.

The current `artifacts/linux-x64/` publish output contains framework, .NET, font, and native binaries but no license or third-party-notice files. Before a release, generate and review a `THIRD-PARTY-NOTICES.md` from the exact locked dependency graph and copy the required complete license/notice texts into every archive or installer. Repeat this check independently for each runtime identifier because native payloads differ.

## Recommended clean release repository

Create a new repository with new Git history rather than publishing this historical clone. Assemble it by an explicit allow-list; do not copy the entire checkout and then rely on deletions.

Suggested structure:

```text
LED-Cube-Studio/
  README.md
  LICENSE
  THIRD-PARTY-NOTICES.md
  LedCube.CrossPlatform.sln
  src/
    Directory.Build.props
    LedCube.App/
    LedCube.Audio/
    LedCube.Core/
    LedCube.Hardware/
  tests/
    LedCube.Tests/
  packaging/
    linux/
  docs/
```

Include only reviewed current source, tests, packaging, and newly authored documentation. Exclude:

- this repository's `.git` directory and inherited history;
- `8x8x8-LED.Core/`, `8x8x8-LED.WinForm/`, the legacy solution, and legacy root documentation;
- all of `Firmware/` for v1;
- `3D Printed Parts/`, `Examples/`, `Schematics/`, and `Screenshots/`;
- legacy and legacy-derived icons;
- NuGet caches, `packages/`, `bin/`, `obj/`, `artifacts/`, and other generated outputs;
- modified legacy files, even where the local modification itself is useful elsewhere.

The new README may link to the original repository for historical context and state hardware/protocol compatibility. It should clearly say that the new project does not include or relicense the original software, firmware, artwork, or hardware design files.

## Chosen licensing plan

The clean standalone repository licenses the project's independently authored code and original branding under **GPL-3.0-or-later**. Exact-version third-party notices and license materials must still be completed for each Linux and Windows binary artifact before publication.

The project license must not claim ownership of third-party libraries, fonts, native components, or separately linked firmware. Keep their notices and licenses intact. Optional SPDX headers can make the boundary clearer in files with unusual provenance, but they are not a substitute for resolving authorship.

## Release-readiness checklist

- [x] Record the project-owner attestation resolving FFT/visualizer provenance uncertainty.
- [x] Replace the production legacy-derived RGB encoder using an independently measured protocol specification and new tests.
- [x] Verify the replacement encoder on physical hardware.
- [x] Replace the legacy-derived application icon with independently authored artwork.
- [x] Create a clean standalone LED Cube Studio repository using the reviewed allow-list.
- [x] Add a **GPL-3.0-or-later** `LICENSE` for independently authored project code in that clean repository.
- [ ] Generate exact-version third-party notices and complete license material for Linux and Windows release artifacts.
- [ ] Keep firmware and inherited legacy assets, code, and history outside v1.

## Audit limitations

This review used the contents and metadata available in the local checkout and public upstream license pages. It cannot determine private agreements, off-repository authorship, or whether every binary contains additional notice requirements. NuGet package declarations are useful evidence but should be verified against the exact files bundled in each final artifact. A later upstream license addition would also need careful scope and commit-history review before it could be applied to older material.
