# Legends of Equestria Launcher

Official launcher for the Legends of Equestria game, built with Avalonia and .NET 8. The launcher keeps game clients up to date via zsync incremental patching and provides quick access to official news, changelogs, and community links.

## Building

- Install the .NET 8 SDK.
- Restore and build with `dotnet build`.
- Run tests with `dotnet test`.

Platform-specific packaging scripts live under `.github/scripts` and are invoked by the GitHub Actions workflows. They expect platform certificate credentials to be provided through environment variables; community builds can run the scripts without those variables for unsigned artifacts.

## Flatpak Build

- Install Flatpak tooling (`flatpak install flathub org.freedesktop.Platform//23.08 org.freedesktop.Sdk//23.08`).
- Build with `flatpak-builder --force-clean build-dir com.loe.Launcher.json`.
- The resulting bundle matches the Flathub manifest used in CI.

## Privacy

The launcher does not collect analytics or telemetry and never stores platform credentials. Network calls are limited to Legends of Equestria patch distribution and content endpoints configured in the source.

## Licensing

Source code in this repository is licensed under the MIT License (see `LICENSE`). Launcher artwork, logos, and other brand assets are © Legends of Equestria and may have additional usage restrictions; substitute your own assets if redistributing.

Third-party dependencies and their licenses are listed in `THIRD-PARTY-NOTICES.md`.
