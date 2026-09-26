# LED Cube Studio

LED Cube Studio is a cross-platform desktop application for driving and visualizing audio on compatible LED cube hardware. The current pre-release supports the 8×8×8 RGB cube on Windows and Linux, including audio-reactive visualizers, serial-device discovery, and hardware diagnostics.

Firmware is not included. Compatibility with a physical cube does not imply ownership or relicensing of its original firmware, software, artwork, or hardware design.

## Build and run

Install the .NET 8 SDK, then run:

```sh
dotnet restore LedCube.CrossPlatform.sln
dotnet build LedCube.CrossPlatform.sln -c Release
dotnet run --project src/LedCube.App/LedCube.App.csproj
```

Linux audio capture requires `pactl` and `parec`, normally provided by `pulseaudio-utils` or a compatible PipeWire-Pulse installation. A self-contained Linux build can be created with `./packaging/linux/publish-linux.sh`.

## License

The independently authored LED Cube Studio project code and original branding are licensed under **GPL-3.0-or-later**. See [LICENSE](LICENSE). Third-party libraries, fonts, and native components retain their own licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and [licenses/](licenses/).
