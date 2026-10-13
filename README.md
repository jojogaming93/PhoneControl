<div align="center">

<img src="assets/branding/phonecontrol-logo.png" alt="PhoneControl pilot mascot" width="140">

# PhoneControl

**Your phone screen. Your keyboard and mouse.**

Mirror your Android phone on Windows and build a touch-control layout that fits your HUD.<br>
Free, open source, and built with a native Windows interface.

[**Download an installer**](https://github.com/jojogaming93/PhoneControl/releases) · [Quick start](#quick-start) · [Controls](#controls-at-a-glance) · [iPhone setup](docs/iphone.md) · [Field guide](docs/README.md)

</div>

---

## What you can do

- **Mirror over USB** with scrcpy's native video and audio engine.
- **Map your keyboard and mouse** to movement, camera swipes, and touch actions.
- **Edit controls on the screen** by dragging markers onto your phone's HUD.
- **Make the layout yours** with configurable keys, sensitivity, joystick reach, and JSON profile import/export.
- **Switch layouts for driving** using local visual HUD recognition.
- **Try the experimental iPhone viewer** for screenshots, taps, swipes, long presses, and Home.

## Get PhoneControl

Open [GitHub Releases](https://github.com/jojogaming93/PhoneControl/releases) and download the **`PhoneControl-Setup-…-win-x64.exe`** installer. Each release also provides a checksum, readable source ZIP, and build manifest.

**Requires:** Intel/AMD x64 Windows 11 24H2 or newer. The installer includes the application runtimes and USB tools; you do not need to install .NET separately.

Preview installers are labelled **alpha**, **beta**, or **RC**. They install as **PhoneControl Preview**, with separate folders, shortcuts, and settings from the regular PhoneControl installation. All preview channels share one Preview installation.

| Phone | Connection | Available controls |
|---|---|---|
| **Android** | USB, with debugging enabled and authorized | Video/audio mirroring, keyboard/mouse mappings, and direct mouse touch |
| **iPhone — experimental** | A dedicated, already running WebDriverAgent endpoint | Screenshot viewer, complete tap/swipe/long-press gestures, and Home |

Android discovery supports multiple vendors; Samsung USB driver repair is available when needed. Other handsets and HUDs need separate testing and calibration.

iPhone support needs a prepared WDA runner, typically using a Mac. Continuous gameplay mapping and audio are unavailable on iPhone, and real iPhone acceptance remains unverified. See the [iPhone setup and testing guide](docs/iphone.md).

## Quick start

1. **Install and open PhoneControl.** Use the desktop shortcut created by the installer.
2. **Prepare your Android phone.** Connect a data-capable USB cable, unlock the phone, enable USB debugging, and accept the computer's debugging prompt.
3. **Connect.** A single authorized USB phone connects automatically. If several devices are detected, select yours and click **Connect phone**.
4. **Focus the phone view.** Press **Left Alt** to capture the mouse for aiming. Press **Esc** to release it.
5. **Calibrate your layout.** Press **F8** to show the mapping overlay, drag markers onto the right HUD buttons, and save your profile on the **Controls** page.

In normal mouse mode, click and drag to send touch input; right-click sends Back. Mapped inputs apply while the phone view is foreground. Switching apps, pausing, editing, or disconnecting releases held touches and the captured cursor.

> The starter profile is a landscape reference layout. Adjust its positions to match your own screen, HUD, and game mode.

## Controls at a glance

| Shortcut | What it does |
|---|---|
| **Left Alt** | Capture or release the mouse for aiming |
| **Esc** | Release aiming and restore the cursor |
| **F8** | Show or hide the editable mapping overlay |
| **F9** | Pause or resume mapped controls |
| **F11** | Toggle the phone view's fullscreen mode |

<details>
<summary><strong>Starter gameplay bindings</strong></summary>

These are starter defaults. Your edited, saved profile remains your own.

| PC input | Phone action |
|---|---|
| WASD | Movement joystick |
| Mouse movement, while captured | Camera swipe |
| Left mouse | Fire / melee — hold |
| Right mouse | Toggle aim down sights — tap |
| Space | Jump |
| C | Crouch |
| Ctrl | Dodge / roll |
| R | Reload |
| Shift + forward movement | Sprint by extending the existing joystick touch |
| = | Autorun — tap the separate HUD button |
| E | Contextual interact |
| Q | Swap weapon |
| B | Backpack |
| M | Map |

**Sprint:** the starter extends normal joystick reach from 9% to 18% of screen height. Releasing Shift returns the same finger to normal reach; Shift alone does not start running. Change the sprint key and reach on the Controls page. Older saved profiles migrate the former Shift/autorun binding to = while preserving other keys and HUD positions.

**Hold** lasts while the PC button is held. **Tap** sends one 40 ms press. Interact is contextual and may need calibration when its HUD button appears.

</details>

<details>
<summary><strong>Custom bindings and input limits</strong></summary>

Supported bindings include A–Z, 0–9, Equals, Space, Shift, Ctrl, RightAlt, Tab, Enter, Backspace, arrow keys, F1–F7, F10, F12, and MouseLeft/Right/Middle/4/5.

Esc, Left Alt, F8, F9, and F11 are reserved. The sprint key is configured separately and cannot also be an action binding. Duplicate bindings are rejected. You can configure up to 64 actions, with a maximum of 10 simultaneous touch fingers.

Renderer modifier shortcuts use Right Windows instead of Alt, so Alt+Q does not quit the phone view. Close it with the window's × or the dashboard's **Disconnect** button.

</details>

## Supported games

- **Undawn** — the only game currently tested with the supplied starter layout and driving HUD recognition. Other games require custom mappings and separate testing.

Each game determines its own input and device rules; mode eligibility is not guaranteed. PhoneControl provides touch translation and does not implement game memory editing, device-identity changes, repeated-firing macros, or gameplay bots.

## Performance

Android defaults are **H.264 · 1920-pixel maximum edge · 16 Mbps · 120 FPS cap · 0 ms added video buffering**. Adjust the stream settings and reconnect to apply them.

The FPS counter reports delivered frames. A higher cap cannot make a 30/60 FPS game render faster, and zero added buffering does not mean zero total latency. Input-to-display latency has not been independently measured.

<details>
<summary><strong>Timing, tuning, and troubleshooting</strong></summary>

- Windows Raw Input reads mouse motion independently of held buttons. Gestures update every 8 ms (125 Hz) through a persistent TCP socket over the USB ADB tunnel.
- Movement, aiming, and actions have distinct touch IDs. Camera swipe renewal preserves held fire and scope touches.
- Relative mouse input bypasses Windows pointer acceleration. Tune the sensitivity slider to preference.
- Audio uses a 30 ms buffer and can be disabled.
- Historical testing delivered approximately 60 FPS in gameplay and 15 FPS under idle touch protection. Dismiss the handset's idle overlay before testing controls.
- Lower capture resolution if encoding drops frames or the phone overheats. Physical handset touches can interfere with emulated multitouch.

See [controls and calibration](docs/controls.md) and [verification evidence](docs/verification.md) for the measured findings and remaining unknowns.

</details>

## Privacy and local storage

Android mirroring and control run locally over USB using ADB and scrcpy. Driving recognition processes images on the PC. PhoneControl has no account, telemetry, analytics service, global keyboard hook, or keylogger; unrelated keys are never forwarded.

The optional iPhone connection sends screenshot requests and your selected gestures to the WDA endpoint you configure. Dependency installation and driver repair download official software from their providers. Android USB debugging authorization stays under the phone owner's control.

| Installation | Profiles and diagnostic logs |
|---|---|
| PhoneControl | `%LOCALAPPDATA%\PhoneControl` |
| PhoneControl Preview | `%LOCALAPPDATA%\PhoneControl Preview` |

Settings are stored in `settings.json`; `phonecontrol.log` rotates at 4 MB. PhoneControl does not upload your phone images, mappings, or logs to an account or analytics service.

## Explore the documentation

| Guide | Start here for… |
|---|---|
| [Field guide](docs/README.md) | Documentation index and engineering reference |
| [Offline website](docs/index.html) | Downloaded guide, interactive HUD atlas, and searchable chapters |
| [Controls](docs/controls.md) | Bindings, sprint, aiming, calibration, and saved profiles |
| [iPhone](docs/iphone.md) | WDA setup, supported gestures, and hardware-free testing |
| [Installation and releases](docs/distribution.md) | Installer behavior, releases, and validation gates |
| [Architecture](docs/architecture.md) | Phone backend ports and adapters |
| [Maintenance](docs/maintenance.md) | Build commands, source map, and Android server rebuilds |

The offline website is included in the installer. Open its local `index.html` to use it; GitHub's source view displays the HTML file rather than hosting that website.

## Build from source

Built with **WinUI 3, Windows App SDK 2.5.1, .NET 10, and scrcpy 4.1**. The .NET SDK is needed for development; published installers include their runtimes.

```powershell
# Download the verified official scrcpy distribution if needed.
.\Install-Dependencies.ps1

dotnet build .\src\PhoneControl\PhoneControl.csproj -c Release
.\scripts\Publish.ps1 -DesktopShortcut
```

A portable publish is written to `artifacts/PhoneControl`. Keep the entire folder together and run `PhoneControl.exe`. Installer builds use `scripts/Build-Installer.ps1`; see [maintenance](docs/maintenance.md) for the additional build tools and test commands.

<details>
<summary><strong>Tests, packaging, and architecture</strong></summary>

Run the published app's regression suite:

```powershell
$test = Start-Process .\artifacts\PhoneControl\PhoneControl.exe -ArgumentList --self-test -WindowStyle Hidden -Wait -PassThru
Get-Content .\artifacts\PhoneControl\self-test-results.txt
$test.ExitCode
```

Tests cover packet encoding, pointer IDs, release pressure, coordinate clamping, profile validation, joystick diagonals, raw mouse input, and camera renewal with held actions over a local TCP connection. Live device and UI acceptance require separate evidence.

The Android backend uses the official scrcpy video/audio client with control disabled, plus a control-only server through an isolated loopback ADB forward. The modified control server rotates renewed swipes through Android touch IDs 0–9 while held fingers keep their IDs. Its prebuilt JAR, source, license, and rebuild instructions are included. The JVM allocator regression covers 128 camera renewals and new action presses.

Phone discovery, sessions, capture, input, and vendor-driver repair use ports and adapters. The WinUI dashboard and native mapping overlay keep the video window separate so the renderer retains its native decode, resizing, and fullscreen behavior.

Installer builds obfuscate the application DLL and test it before packaging. `Publish.ps1 -Obfuscate` does the same for a portable publish. Ordinary builds retain debugging symbols. Private originals and rename mappings stay under ignored `artifacts/obfuscation`; readable corresponding source and build configuration are included with every release. Obfuscation does not prevent decompilation or restrict rebuilding.

</details>

## License and source

Copyright (c) 2026 [jojogaming93](https://github.com/jojogaming93). Original PhoneControl code, documentation, and assets are licensed under [GNU AGPL version 3 only](LICENSE) (`AGPL-3.0-only`), without warranty. You may use, modify, and redistribute them under that license. Distributed derivatives must provide corresponding source, and modified versions used to interact with users over a network must offer those users corresponding source.

Every installer and published folder includes `source/PhoneControl-source.zip`; **About & license** opens the archive. See [source and rebuild instructions](SOURCE.md) and [third-party notices](THIRD-PARTY-NOTICES.md), including the Apache-2.0 control server.

Public diagrams and regression images use anonymous coordinates and arrow templates. Private captures, device identifiers, local settings, and credentials are excluded. [Public-release preparation](PUBLIC-RELEASE.md) describes the source privacy checks.
