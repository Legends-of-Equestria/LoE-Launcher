# Legends of Equestria Launcher

Official launcher for the Legends of Equestria game, built with Avalonia and .NET 10. The launcher keeps game clients up to date via zsync incremental patching and provides quick access to official news, changelogs, and community links.

## Download

Download the latest launcher from:
- **Official website**: https://www.legendsofequestria.com/downloads
- **GitHub Releases**: https://github.com/Legends-of-Equestria/LoE-Launcher/releases

## Building

- Install the .NET 10 SDK.
- Restore and build with `dotnet build`.
- Run tests with `dotnet test`.

Platform-specific packaging scripts live under `.github/scripts` and are invoked by the GitHub Actions workflows. They expect platform certificate credentials to be provided through environment variables; community builds can run the scripts without those variables for unsigned artifacts.

## Flatpak Build

- Install `flatpak` and `flatpak-builder` from your distribution.
- Run `bash .github/scripts/build-flatpak.sh`. It publishes the launcher, installs the Flatpak SDK for your user if needed and writes the bundle to `Publish/Flatpak/LoE-Launcher-linux.flatpak`, the same way CI does.

## Privacy

The launcher does not collect analytics or telemetry and never stores platform credentials. Network calls are limited to Legends of Equestria patch distribution and content endpoints configured in the source.

## Licensing

Source code in this repository is licensed under the MIT License (see `LICENSE`). Launcher artwork, logos, and other brand assets are © Legends of Equestria and may have additional usage restrictions; substitute your own assets if redistributing.

Third-party dependencies and their licenses are listed in `THIRD-PARTY-NOTICES.md`.
