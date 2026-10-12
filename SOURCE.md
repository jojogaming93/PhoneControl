# PhoneControl corresponding source

Copyright (c) 2026 [jojogaming93](https://github.com/jojogaming93).
PhoneControl's original code is licensed under the **GNU Affero General Public
License version 3 only**, SPDX identifier `AGPL-3.0-only`. The full, unmodified
license text is in `LICENSE`. PhoneControl is provided without warranty; see the
license for the applicable terms. You may use, modify and redistribute it under
that license. Third-party code, runtimes and artwork retain their respective
notices and licenses; see `THIRD-PARTY-NOTICES.md`.

## Obtain the source for this release

Every published portable folder and installer contains
`source/PhoneControl-source.zip`, the editable PhoneControl source and build/install
scripts for the included application. Open **About & license → Open corresponding
source**, or copy the ZIP from the installation directory and extract it to a
writable folder. The release output also includes the identical archive named
`PhoneControl-<version>-source.zip`, to serve alongside the downloadable installer.
No GitHub access or Internet connection is needed to obtain the bundled source.

`SOURCE-MANIFEST.json` inside the ZIP records the release version, original Git
commit, working-tree status and a hash for every included source file. The archive
captures the current working tree, including uncommitted edits and new source
files; it does not substitute an older committed snapshot. It includes the
original readable C# sources, XAML, project/configuration files, scripts,
obfuscation configuration, installer source, documentation/fixtures and Android
allocator source/license/prebuilt asset. Generated app binaries, debug symbols,
private obfuscation logs/mappings, user profiles and Git credentials are excluded.

## Build and modify

Use Windows x64 and the .NET SDK selected by `global.json` (10.0.302 with
latestFeature roll-forward). For the self-contained installer, install the Visual
Studio C++ x64 build tools and Windows SDK as described in `docs/maintenance.md`.
Build tools and package restoration may need Internet access. In an extracted
archive, first run `git init` so the source-packaging script can enumerate files;
no remote, account, commit or credentials are required.

```powershell
git init
python docs/build.py
./Install-Dependencies.ps1
dotnet build ./src/PhoneControl/PhoneControl.csproj -c Release
./scripts/Publish.ps1 -Version <version-from-SOURCE-MANIFEST.json>
./scripts/Build-Installer.ps1 -Version <version-from-SOURCE-MANIFEST.json>
```

Substitute the numeric release version, such as `1.0.5`, for the placeholder.
`Publish.ps1` produces readable, unobfuscated binaries by default; `-Obfuscate`
applies the shipped Obfuscar configuration. The installer builder uses that
configuration before packaging and bundles the corresponding readable
source regardless of obfuscation. Source availability is required by the license;
obfuscation does not restrict your right to modify or rebuild the program.

The control JAR is prebuilt from scrcpy 4.1; using/building the Windows app needs
no Java or Android SDK. To modify the control server, follow
`server/README.md` and `scripts/Build-ControlServer.ps1` with JDK 21+ and the
Android SDK. That script verifies the upstream scrcpy source download, applies
the included allocator replacement, tests it and rebuilds the asset.

## Dependencies and upstream source

- scrcpy 4.1 and the control-server base: Apache-2.0;
  [versioned source](https://github.com/Genymobile/scrcpy/tree/v4.1).
- Windows App SDK / WinUI 3 2.5.1: Microsoft and contributors;
  [Windows App SDK source](https://github.com/microsoft/WindowsAppSDK) and
  [WinUI source](https://github.com/microsoft/microsoft-ui-xaml).
- .NET 10: [.NET runtime source](https://github.com/dotnet/runtime) and
  [SDK source](https://github.com/dotnet/sdk).
- The unmodified scrcpy tools distribution retains its own `LICENSE.txt` covering
  ADB, SDL, FFmpeg and libusb. Runtime notices produced by .NET publishing are
  retained in the payload.
- Obfuscar.GlobalTool 2.2.49 is a build tool, not an installed app dependency;
  [source](https://github.com/obfuscar/obfuscar/tree/2.2.49).

This AGPL release imposes no additional permission requirement beyond the license
terms.
