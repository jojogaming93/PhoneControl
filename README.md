# PhoneControl

<img src="assets/branding/phonecontrol-logo.png" alt="PhoneControl pilot mascot" width="160">

A free native Windows app for USB Android mirroring and editable keyboard/mouse touch mappings. Built with **WinUI 3, Windows App SDK 2.5.1, .NET 10**, and the unmodified **scrcpy 4.1** video engine.

## Copyright and license

**Copyright (c) 2026 [jojogaming93](https://github.com/jojogaming93).** PhoneControl's original code, documentation and assets are licensed under the [GNU Affero General Public License version 3 only](LICENSE) (`AGPL-3.0-only`). You may use, modify and redistribute PhoneControl under that license. Distributed derivatives must provide corresponding source under the AGPL, and modified versions used to interact with users over a network must offer those users their corresponding source. The software is provided without warranty; see the license for details. Third-party components retain [their license notices](THIRD-PARTY-NOTICES.md), including the Apache-2.0 control server.

Every published folder and installer bundles [corresponding source and build instructions](SOURCE.md) in `source/PhoneControl-source.zip`. **About & license** displays the running version, owner, license, warranty notice and a button to open that archive. The same source ZIP is emitted alongside the installer for release downloads.

## Supported games

- **Undawn** — the only game currently tested with the supplied starter layout and driving HUD recognition. Other games require custom mappings and separate testing.

## Code signing policy

SignPath Foundation application preparation is pending; PhoneControl is not yet signed by or sponsored by SignPath Foundation. The proposed maintainer, reviewer and release approver is [jojogaming93](https://github.com/jojogaming93). Public releases must originate from the audited public source and approved build workflow, include corresponding source, and pass regression and Authenticode verification. No self-signed certificate or unsigned validation artifact is a trusted public release. If accepted, release signing will use the Foundation's managed service and manual approval; the required provider attribution will be added after acceptance. See the [application draft](docs/signing-application.md).

## Privacy

PhoneControl mirrors and controls the phone locally over USB using ADB and scrcpy. Profiles and diagnostic logs stay on the PC in `%LOCALAPPDATA%\PhoneControl`; driving recognition processes screen images locally. PhoneControl has no account, telemetry or analytics service and does not upload phone images, mappings or logs. Dependency installation and driver repair download official software; those downloads contact their providers. Android's USB authorization remains under the phone owner's control. Source publishing, support requests and signing applications are separate maintainer actions.

## Documentation

Open [the PhoneControl field guide](docs/index.html) for the offline documentation website, interactive HUD atlas and debugging evidence. [The Markdown index](docs/README.md) links the reusable game, controls, protocol, verification and lessons-learned references for future agents.

## Run

Public release installers belong in `artifacts/releases`. Version 1.0.6 requires trusted signing before public distribution; signing-provider approval is pending. Development/CI installers built with `scripts/Build-Installer.ps1 -AllowUnsigned` are isolated in `artifacts/unsigned-validation` and must not be published as trusted releases. The installer bundles all application runtimes/tools for the current user and creates shortcuts. The target is Intel/AMD x64 Windows 11 24H2 or newer. [Installation and release notes](docs/distribution.md) describe phone consent, driver repair and release gates. Installer binaries are generated artifacts rather than source-controlled files.

Open `artifacts/PhoneControl/PhoneControl.exe` or the **PhoneControl** desktop shortcut. The published folder contains the .NET and Windows App SDK runtimes and a copy of scrcpy, so no paid license or separate runtime installation is required. Keep the entire folder together.

1. Connect the phone with a data-capable USB cable. Unlock it, enable USB debugging, and accept this computer's debugging prompt.
2. An authorized USB phone connects automatically when it is the only detected device. With several devices, select your phone and click **Connect phone**. A separate phone view opens with GPU rendering (Direct3D 11 on this PC).
3. Click the phone view to focus it. **Left Alt** captures/releases the mouse for aiming and hides the cursor while captured. **Esc** releases it and restores the cursor. Switching windows also releases aiming. Normal mouse mode supports click-and-drag touch and right-click Back.
4. **F8** toggles the mapping overlay. Click/drag a marker, or select **Pick** beside an action and click its button on the phone view. Coordinates are percentages, independent of stream resolution. **F9** pauses/resumes controls. **F11** toggles fullscreen.
5. Save a profile on the **Controls** page. You can import/export a JSON profile, rename keys, add/delete actions, move the joystick/look anchors, and change sensitivity or joystick radius.

Game inputs apply only while the phone view is foreground. Switching apps, pausing, editing, or disconnecting releases held touches and cursor confinement. No global keyboard hook or keylogger is installed. Unrelated keys are never forwarded to the phone.

## Starter profile

The supplied positions are a landscape reference layout. Adjust them when your HUD, weapon or mode changes. Interact is contextual and may need placement when its button appears.

| PC input | Phone action |
|---|---|
| WASD | Movement joystick |
| Mouse movement, after Left Alt | Camera swipe |
| Left mouse | Fire / melee (hold) |
| Right mouse | Toggle aim down sights (tap) |
| Space | Jump |
| C | Crouch |
| Ctrl | Dodge / roll |
| R | Reload |
| Shift + forward movement | Sprint by extending the movement joystick |
| = | Autorun (tap the HUD button) |
| E | Contextual interact |
| Q | Swap weapon |
| B | Backpack |
| M | Map |

Supported bindings: A–Z, 0–9, = (Equals), Space, Shift, Ctrl, RightAlt, Tab, Enter, Backspace, arrow keys, F1–F7, F10, F12, MouseLeft/Right/Middle/4/5. Esc, Left Alt, F8, F9 and F11 are reserved. The sprint key is configured in the Sprint card on Controls and cannot also be an action binding. Duplicate bindings are rejected. Up to 64 actions can be configured, with a maximum of 10 simultaneous fingers.

Hold Shift while moving forward to extend the existing joystick touch from normal reach (9% of screen height in the starter layout) to sprint reach (18%). Releasing Shift returns that same finger to normal reach; releasing movement lifts it. Shift alone does not start running. Change the sprint key and reach on the Controls page. Older saved profiles automatically move the former Shift/autorun mapping to =, preserving your other keys and HUD positions.

Alt+Q no longer invokes the renderer's quit shortcut; scrcpy's modifier shortcuts are assigned to Right Windows instead of Alt. Use the window's × or the dashboard's Disconnect button to close the phone view. F11 still toggles fullscreen.

**Hold** lasts while the PC button is held. **Tap** sends one 40 ms press. No repeated firing macros, game memory editing, device-identity spoofing, or automation bots are implemented.

## Performance

- Default: H.264, 1920-pixel maximum edge, 16 Mbps, 120 FPS cap, **0 ms added video buffering**.
- Windows Raw Input reads mouse motion independently of held buttons and preserves key/button press edges; gestures are updated every 8 ms (125 Hz) and serialized directly to a persistent TCP socket through the USB ADB tunnel. Movement, aim, and actions have distinct touch IDs.
- Audio uses a 30 ms buffer and can be disabled. Stream settings apply after reconnecting.
- The FPS counter reports actual mirrored frames. The FPS cap cannot make a 30/60 FPS game render faster. Increase the game's FPS option if available; reduce capture resolution if encoding drops frames or the phone overheats.
- Historical tests delivered approximately 60 FPS in normal gameplay and approximately 15 FPS under idle touch protection. Dismiss the handset's idle overlay before testing input.
- Input-to-display latency has not been measured with a high-speed camera. Zero added buffering does not mean zero total latency: capture, encoding, USB, decoding, monitor refresh, and the game all contribute.
- Mouse aiming uses raw mouse deltas while the hidden cursor remains confined. Relative mouse input bypasses Windows pointer acceleration; tune the app sensitivity slider to preference. Camera swipe renewal keeps fire and scope touches held.
- Physical handset touches during play can interfere with emulated multitouch; use the PC input while playing.

## Build / publish

GitHub Actions builds/tests the installer and automatically attaches the setup `.exe`, source ZIP, checksum and manifest to a release draft when a version tag (`1.0.8` or `v1.0.8`) is pushed or **Build Windows installer and release** is run manually. Drafts remain unsigned validation builds while trusted signing is pending; they are visible to repository maintainers. See [automatic release guidance](docs/distribution.md#automatic-github-release-drafts).

The .NET 10 SDK is required to build, not to run the published folder. Versions are pinned for reproducibility; stable dependencies can be updated intentionally later.

```powershell
# Only needed if scrcpy 4.1 is not already available:
.\Install-Dependencies.ps1
dotnet build .\src\PhoneControl\PhoneControl.csproj -c Release
.\scripts\Publish.ps1 -DesktopShortcut
```

The dependency installer downloads only the official GitHub scrcpy 4.1 release and verifies its published SHA-256 digest. Publishing copies the full scrcpy distribution and its license notices into the app folder.

`scripts/Build-Installer.ps1` obfuscates the release's PhoneControl DLL before running its regression suite and packaging it. For an obfuscated portable folder, use `scripts/Publish.ps1 -Obfuscate`; ordinary builds/publishes keep readable symbols for debugging. Private originals/rename mappings remain in ignored `artifacts/obfuscation` and are not shipped, while the readable corresponding source and obfuscation configuration are always bundled under the AGPL. Obfuscation does not restrict modification/rebuilding or prevent decompilation. With a trusted certificate, pass `-SignTool` and `-CertificateThumbprint` to the installer builder to sign and verify the app EXE/DLL and installers after obfuscation. Signing identifies the publisher and detects changes; it does not hide code. See [release guidance](docs/distribution.md).

```powershell
$test = Start-Process .\artifacts\PhoneControl\PhoneControl.exe -ArgumentList --self-test -Wait -PassThru
Get-Content .\artifacts\PhoneControl\self-test-results.txt
$test.ExitCode
```

Tests cover control-packet encoding and release pressure, pointer IDs, coordinate clamping, profile roundtrip/validation, and circular joystick diagonals. Live device and UI tests are separate from these deterministic tests.

The tests also exercise the camera controller with fire and scope held over a real local TCP connection, verify swipe renewal and cleanup, check raw relative/absolute mouse motion, and check the ten-finger cap.

Samsung Game Booster can dim and lock game touch input after idle time. If its touch-protection lock appears, dismiss it on the handset before continuing. The overlay can also lower delivered FPS even while the mirroring connection remains healthy.

## Architecture and maintenance

`DeviceSession` manages two scrcpy sessions: the official GPU-rendered video/audio client with control disabled, and a control-only Android server speaking scrcpy 4.1's documented binary protocol. A unique loopback ADB forward isolates the control channel. Temporary server JARs and the app's own forward are removed on a normal disconnect.

Phone discovery, sessions, input, frame capture and USB driver repair use [ports and adapters](docs/architecture.md). The current Android backend discovers devices across vendors; Samsung driver repair is one registered adapter. Other HUDs need calibration, other handsets remain unverified, and iPhone support requires a separate backend. No phone-specific display fallback or saved-profile replacement is used.

`InputEngine` handles focus, key edges, independent fingers, joystick diagonals, tap timing, camera swipes, and cursor release. `MappingOverlay` is a lightweight native layered window, leaving video frames in scrcpy's native decode/render path. The settings/dashboard interface is WinUI 3. The video window is deliberately separate so native fullscreen and resizing keep working.

The control channel uses a small fork of the scrcpy 4.1 server that rotates restarted swipes through available Android touch IDs 0..9. Held action fingers retain their IDs. Keeping every gesture inside these ten slots resolved periodic input loss in the tested HUD. The video/audio server remains official and unmodified. The prebuilt control JAR is included in `assets`; its modified source, license and rebuild instructions are in `server`. `scripts/Build-ControlServer.ps1` rebuilds it with a JDK and Android SDK and runs allocator tests, including 128 camera restarts and new action presses with fire/scope held.

Profiles/settings: `%LOCALAPPDATA%\PhoneControl\settings.json`. Diagnostics: `%LOCALAPPDATA%\PhoneControl\phonecontrol.log` (rotated at 4 MB). No account or external service is needed during use. Debugging authorization is managed on the phone.

The app uses standard Android touch injection. Each game determines its own input and device rules; mode eligibility is not guaranteed.

## Public source

The repository is [jojogaming93/PhoneControl](https://github.com/jojogaming93/PhoneControl). [Public-release preparation](PUBLIC-RELEASE.md) describes the clean history, privacy checks and source-only upload package. Public diagrams and regression images are generated from anonymous coordinates/arrow templates; private game/device captures and local settings are excluded.
