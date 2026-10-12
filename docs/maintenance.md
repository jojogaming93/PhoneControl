# Maintenance and agent workflow

**Current naming/version:** PhoneControl 1.0.7. Build `src/PhoneControl/PhoneControl.csproj`; launch with `Launch-PhoneControl.ps1`. The matching control JAR and branding assets use lowercase `phonecontrol` filenames. The Android binary bytes are unchanged. The stable installer app ID enables saved-settings import from its previous registered directory; settings copies never overwrite the original or existing new settings.

## Source map

| Source | Responsibility |
|---|---|
| src/PhoneControl/Profile.cs | Starter layout, validation, key names, settings, sprint migration, log |
| src/PhoneControl/PhonePorts.cs | Backend, session, input, mirror, capture and driver contracts |
| src/PhoneControl/AndroidPhoneBackend.cs | Android device discovery, state/transport normalization and session factory |
| src/PhoneControl/DeviceSession.cs | Android adapter: scrcpy version, ADB sessions, geometry, binary packets, cleanup |
| src/PhoneControl/PhonePortsTest.cs | Simulated alternate backend and Android discovery/geometry regressions |
| src/PhoneControl/InputEngine.cs | Focus, modes, keys/edges, tap timing, movement, action ownership |
| src/PhoneControl/AimGesture.cs | Camera swipe envelope/renewal and raw-motion accumulator |
| src/PhoneControl/RawInputReader.cs | WM_INPUT keyboard/mouse parsing and Alt differentiation |
| src/PhoneControl/MappingOverlay.cs | Mapping markers, no-activate cursor-hiding overlay |
| src/PhoneControl/Native.cs | Win32 methods, video rectangle, cursor/focus helpers |
| src/PhoneControl/MainWindow.cs | WinUI dashboard, settings, profile editing and session lifecycle |
| src/PhoneControl/SelfTest.cs | 40 deterministic/local TCP checks |
| server/PointersState.java | Compatible Android local-ID rotation |
| server/test/PointerStateTest.java | Real allocator regression with field-only Android test double |
| scripts/Build-ControlServer.ps1 | Verified upstream source, Java/AIDL compilation, JVM checks, D8 JAR |
| scripts/Publish.ps1 | Portable Windows output, scrcpy tools, notices, desktop shortcut |
| docs/build.py | Offline website generator from these Markdown chapters and HUD JSON |

## Build the Windows app

The app targets x64 Windows, .NET 10 and WinUI 3 with Windows App SDK 2.5.1. `global.json` pins SDK 10.0.302 with latestFeature roll-forward. The project declares target framework Windows 10.0.26100.0 and minimum platform 10.0.17763.0; this declaration is not a test of every supported Windows version.

```powershell
.\Install-Dependencies.ps1
dotnet build .\src\PhoneControl\PhoneControl.csproj -c Release
.\scripts\Publish.ps1 -DesktopShortcut
```

The installer obtains official scrcpy 4.1 and checks the release asset's published SHA-256. Publishing bundles .NET/Windows App SDK runtimes, scrcpy and notices. Windows build/publish uses the included `assets/phonecontrol-control.jar` and needs no Java or Android SDK.

Close the running published app normally before publishing to avoid locked files. A new JAR needs a reconnect: changing the local asset does not replace the server already running on Android. Verify the asset and published JAR hashes match. Do not accidentally run an older JAR in `artifacts` left by an experiment.

```powershell
$testRun = Start-Process .\artifacts\PhoneControl\PhoneControl.exe -ArgumentList --self-test -WindowStyle Hidden -Wait -PassThru
Get-Content .\artifacts\PhoneControl\self-test-results.txt
$testRun.ExitCode
Get-FileHash .\assets\phonecontrol-control.jar, .\artifacts\PhoneControl\tools\phonecontrol-control.jar
```

## Rebuild the Android control server

Use JDK 21 or newer and Android SDK platform 36 or newer with appropriate build tools. Script defaults currently select platform `android-37.0` and build-tools `36.0.0`; override parameters to match installed SDK components. The verified session used JDK 21.0.12.1, android-37.0 and build-tools 36.0.0.

```powershell
.\scripts\Build-ControlServer.ps1 -Jdk '<JDK directory>' -AndroidSdk '<Android SDK directory>' -Platform 'android-37.0' -BuildTools '36.0.0'
```

The script downloads scrcpy v4.1 source and verifies SHA-256 `1525C92183C4D1F7D4D1A702CFDDC5A452DA4F49222B0C327A7E8B1E0AF14833`. It replaces only PointersState, generates AIDL and BuildConfig, compiles Java with release 17, runs allocator regression checks, dexes with min-api 21 and writes the prebuilt asset. That output is included by the project as `tools/phonecontrol-control.jar`.

Oracle javapath shims can obscure the real JDK location. Supply `-Jdk` or let the script read `java.home`. Keep AIDL include/framework paths in the form accepted by the Windows tool; mixed path separators caused an earlier compilation error. Restore JAVA_HOME after a build, as the script does.

scrcpy protocol/server versions are intentionally pinned. Updating only the renderer, server source or binary message layout is unsafe; update and test both control and video paths deliberately. Licensing/notices are in `THIRD-PARTY-NOTICES.md` and `server/LICENSE.scrcpy`; retain the modified-source header and redistributed notices.

## Documentation workflow

End-user distribution now has a self-contained installer and native launcher. Read [distribution.md](distribution.md) before changing release packaging, Windows support, device onboarding or driver repair. Build with `scripts/Build-Installer.ps1`; lifecycle-test in a disposable account/VM with `scripts/Test-Installer.ps1`. The test intentionally refuses to overwrite an existing registered installation. Keep application settings outside the installed folder and retain them during uninstall.

Edit the Markdown chapters and `data/hud.json`, then run `python docs/build.py`. The resulting `index.html` has embedded chapter HTML and data, with local CSS/JS/images, so double-click opening works without fetch or a web service. No npm package is required.

Use a local loopback HTTP server for browser preview if needed. Verify HUD selection, profile switching, coordinate picking, search, chapter navigation, evidence notes and a narrow viewport. Documentation controls do not inject phone touches or change the player's settings.

Repository source and the prebuilt control JAR are committed. Generated app builds, downloaded scrcpy tools, logs and temporary experiments remain ignored. Public diagrams and synthetic test inputs are reproducible with `python docs/generate_fixtures.py`; private screenshots remain excluded. Keep experimental scripts separate from validated production behavior.

## Useful next investigations

- Measure input-to-display latency with an external high-speed recording and explicit timing methodology.
- Test another HUD/device/aspect ratio and export its profile instead of changing the test handset baseline.
- Test weapon/context changes, vehicles and other movements only in a player-approved context.
- Explore the game's scope setting deliberately and document its relationship with Tap/Hold bindings.
- Determine the minimum sprint reach without assuming the current saved reach is the threshold.
- If another failure appears, record compatible local IDs and test after sustained input before increasing queues or rewriting the transport.

The current implementation does touch translation and screen streaming. It does not inspect game memory, change device identity, automate gameplay decisions or establish mobile-mode eligibility. Keep new knowledge grounded in observations rather than inferred abilities.

## Driving and overlay source additions - 11 October 2026

- `src/PhoneControl/DrivingHudDetector.cs`: visual arrow classification and hysteresis.
- `src/PhoneControl/DrivingModeMonitor.cs`: cancellable background capture/decode loop.
- `src/PhoneControl/MirrorFrameCapture.cs`: reusable foreground mirror capture for fast detection.
- `src/PhoneControl/DrivingDetectionTest.cs`: worker timing/fallback/cancellation and real GDI fixture regressions.
- `assets/reference-driving-hud.json`: embedded templates sampled from the observed example car screenshot.
- `docs/fixtures/`: account-free synthetic classifier/capture inputs; generated by `docs/generate_fixtures.py`.

Publish/installer commands remain unchanged; use version 1.0.1 for this update. The self-test executable needs the bundled docs/fixtures inputs; a repository build can find them by walking ancestor directories. No Java rebuild is needed because the control allocator/JAR did not change.

## Wheel and dropdown update - 11 October 2026

Use version 1.0.2 for the wheel-input and key-selector update. `MouseWheelInput.cs` handles notch accumulation; action dropdowns support multiple selected inputs and preserve their comma-separated names in profile `Key` strings. Source starter keys and historical profile snapshots are unchanged. Local self-tests cover signed/high-resolution wheel deltas, rapid notch timing, profile validation, shared Hold ownership, sustained camera renewals and a new wheel action afterward. Physical gameplay acceptance remains separate.

## Faster driving recognition - 11 October 2026

Release scripts now default to 1.0.3. Foreground mirror recognition runs at a 50 ms target cadence and enters Driving on the first clear match. A serialized phone screenshot fallback continues independently and cannot block mirror detection. The control JAR, profiles and touch protocol are unchanged. Keep source/published/installed checks distinct from live enter-vehicle latency; the self-test renders the real HUD fixtures in a temporary native surface without injecting phone touches.

## Ownership and protected releases - 11 October 2026

**Current 1.0.6:** original pilot mascot assets are in `assets/branding` and used in the dashboard, app and installers. Installer builds write to `artifacts/releases`. Run `python docs/build.py` and the public-source audit before packaging. Audited branding hashes are allowlisted separately from the reproducible HUD fixtures.

Release scripts now default to **1.0.5**. Original PhoneControl code/documentation/assets use `LICENSE` (GNU AGPL version 3 only, `AGPL-3.0-only`) with **jojogaming93** as copyright owner and publisher. This supersedes the temporary proprietary terms introduced in 1.0.4. Third-party licenses and the Apache-2.0 server remain intact. `AppInfo.cs` reads assembly metadata so sidebar/About version labels follow `-Version` overrides without a separate UI version constant. Copyright metadata also appears in app/installer file properties and the installer publisher entry.

`Build-Installer.ps1` now always calls `Publish.ps1 -Obfuscate`. `Obfuscate.ps1` acquires the pinned Obfuscar.GlobalTool 2.2.49 build tool under ignored `tools/obfuscar`, generates absolute-path configuration from `scripts/obfuscar.xml`, and replaces only `PhoneControl.dll`. Private implementation names and strings are obscured; public API, JSON contracts, WinUI/WinRT glue and native layout fields have compatibility exclusions. The regression suite runs on the protected DLL before packaging. Ordinary `dotnet build` and `Publish.ps1` remain unobfuscated for debugging; use `-Obfuscate` for a protected portable publish. No Java rebuild is required.

Original DLLs, generated configuration and rename mappings stay under ignored `artifacts/obfuscation`; retain these privately to interpret release stack traces. They and application PDBs are excluded from the installed payload. Obfuscation adds friction to reverse engineering, not confidentiality or a guarantee against decompilation. It does not protect a separately shared source tree.

`Build-SourceArchive.ps1` packages the current readable sources (including uncommitted edits and new source files), build/install scripts, obfuscation configuration, assets and docs into `source/PhoneControl-source.zip` in every publish. It uses explicit source roots/formats and Git's non-ignored file listing; build output, settings, credentials and private mappings stay excluded. A per-file `SOURCE-MANIFEST.json` records version/commit/status/hashes. `SOURCE.md` supplies extraction/build/dependency instructions; an extracted archive can be rebuilt after `git init` without a GitHub account or a commit. The installer and CI also provide the identical source ZIP beside the release binary. About includes source access, redistribution rights and the warranty notice. Obfuscation does not prevent AGPL recipients from modifying, rebuilding or running the app.
