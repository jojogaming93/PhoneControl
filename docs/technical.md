# Technical control reference

## Components and data path

PhoneControl uses WinUI 3 / .NET 10 for settings and native Win32 input/overlay APIs for gameplay. Official scrcpy 4.1 renders H.264 video/audio in a separate GPU-backed window. A separate Android control-only server is built from scrcpy 4.1 with a modified pointer allocator. The video/audio client and server remain unmodified.

The control path is: Windows Raw Input and key state → InputEngine's 8 ms tick → independent logical fingers → 32-byte touch packets on one persistent TCP stream → loopback ADB forward over USB → Android server → MotionEvent injection → foreground Android application.

The application consumes phone input, lifecycle, capture and discovery ports. `AndroidPhoneBackend` and `AndroidPhoneSession` implement the current ADB/scrcpy adapter; vendor driver repair is registered separately. See [phone backend architecture](architecture.md) for contracts and extension guidance. Display geometry comes from the selected phone's effective `wm size`; invalid dimensions abort connection instead of using a handset-specific fallback.

No per-key `adb shell input` process is used for gameplay. ADB shell commands are useful for setup/probes but are not the low-latency multitouch transport. The control socket uses NoDelay and a 250 ms send timeout. Serialized writes share a lock; there is no parallel touch writer reordering packets.

## Starting the control session

`AndroidPhoneSession.Connect` requires scrcpy version exactly 4.1. It obtains display size using `wm size`, generates a unique session ID, pushes `assets/phonecontrol-control.jar` under an app-owned name in `/data/local/tmp`, and creates a `tcp:0` ADB forward to `localabstract:scrcpy_<hex-session-id>`.

Conceptual command shape, using the actual selected device and session values:

```text
adb -s <serial> push <control-jar> /data/local/tmp/phonecontrol-<scid>.jar
adb -s <serial> forward tcp:0 localabstract:scrcpy_<scid>
adb -s <serial> shell CLASSPATH=<remote-jar> app_process / com.genymobile.scrcpy.Server 4.1
  scid=<scid> video=false audio=false control=true tunnel_forward=true
  send_device_meta=false send_dummy_byte=true clipboard_autosync=false
  power_on=false cleanup=false
```

These are command shapes; the multiline server example is not a ready-to-paste shell invocation. Consult `AndroidPhoneSession.cs` for the actual ArgumentList construction and `0x` formatting. The PC connects to the allocated loopback port and consumes the initial zero dummy byte. It drains the server's outgoing control messages asynchronously.

The renderer is a different scrcpy session with `--no-control --video-codec=h264 --max-fps=120 --max-size=1920 --video-bit-rate=16M --video-buffer=0 --print-fps --shortcut-mod=rsuper`. Audio uses `--audio-buffer=30` or is disabled. The cap/size/bitrate are settings, not immutable constants.

## Touch packet: 32 bytes, big-endian

| Byte offset | Size | Meaning |
|---|---:|---|
| 0 | 1 | Message type 2: inject touch event |
| 1 | 1 | Action: 0 DOWN, 1 UP, 2 MOVE |
| 2 | 8 | Signed 64-bit logical pointer ID |
| 10 | 4 | X physical pixel, clamped to 0..W-1 |
| 14 | 4 | Y physical pixel, clamped to 0..H-1 |
| 18 | 2 | Unsigned physical width |
| 20 | 2 | Unsigned physical height |
| 22 | 2 | Pressure: 65535 for DOWN/MOVE, 0 for UP |
| 24 | 4 | Action-button field: zero for these finger touches |
| 28 | 4 | Buttons field: zero for these finger touches |

`Packet` starts with a zeroed buffer, so both button fields remain zero. Mouse-like pressure/flags should not be substituted for finger fields. Width/height are checked as UInt16. Unknown MOVE/UP logical IDs are suppressed, and a new DOWN is rejected if ten fingers are already active.

Android Back is a different 14-byte key-message sequence: type 0, keycode 4, DOWN then UP, with repeat/metastate zero. It is used by normal right-click, not by aiming right-click.

## Three different pointer numbers

**Logical PC pointer ID:** stable ownership across gesture renewal. Joystick = 0; camera = 1; direct cursor touch = 2; action binding index i = 10+i. A logical action ID of 10 or 11 is valid.

**Android local pointer ID:** allocated by `PointersState` when a logical gesture goes DOWN. It must stay stable until that gesture goes UP. The custom allocator rotates the next available ID through 0–9 and skips every still-held local ID. A renewed camera gesture keeps logical ID 1 but usually receives a different Android local ID.

**MotionEvent pointer index:** array position among currently active pointers. Pointer arrays can compact after an UP. Index is not the Android ID or PC logical ID. Do not fix this bug by permanently forcing camera to array index 0; that experiment did not solve swipe renewal.

Android can represent local IDs above 9, but live tests on this handset ignored camera ID 10 and a later action ID 13 while ID 9 still worked. An initial rotation through 0–31 caused periodic input loss until allocation returned to usable slots. The fix limits all local touch IDs to 0–9. This is a measured compatibility behavior; the engine's internal implementation was not inspected.

## Camera renewal algorithm

Begin places camera logical ID 1 at `(LookX,LookY)`. For a mouse delta `(dx,dy)` and displayed viewport `(width,height)`, normalized movement is `(dx * sensitivity / width, dy * sensitivity / height)`.

Large deltas are split into `ceil(max(abs(sx)/0.05, abs(sy)/0.08))` steps. Before each step, a target more than 0.17 horizontally or 0.25 vertically from the anchor, or outside 0.01–0.99, triggers camera UP at its previous position and DOWN at the anchor. The current step is then consumed as MOVE, with final coordinates clamped to 0–1. Other fingers are not lifted.

The look finger is established before new action fingers in an aiming tick. Rotating local IDs solved the observed renewal problem; the exact role of game frame batching is not proven. With all ten slots occupied, a freed slot may be the only available replacement, so do not promise a distinct replacement under every possible ten-finger state.

## Windows motion, edges and cursor

`RawInputReader` subclasses the WinUI window and registers keyboard/mouse devices with INPUTSINK. InputEngine explicitly gates events to the foreground mirror HWND. Relative motion accumulates signed deltas; absolute raw samples are converted from 0–65535 to desktop pixels and the first sample only establishes a baseline. The accumulator drains once each tick and resets on mode/focus changes.

Raw key/button edges are combined with asynchronous key-state polling. Duplicate observations are filtered over 32 ms. Short movement presses receive a 40 ms representation. Left/Right Alt are differentiated using the raw keyboard E0 flag; Shift/Ctrl variants are normalized.

The aiming overlay is an almost-transparent native layered, no-activate window. Uniform alpha 1 allows it to own mouse hit tests and hide the cursor via WM_SETCURSOR / SetCursor(0), while keyboard focus stays with the mirror. Color-key transparency is used for the edit overlay instead. Esc, leaving aim, switching to the dashboard and disposal restore the arrow and release ClipCursor confinement.

## Cleanup and geometry

Each active logical finger is stored with its latest coordinate. ReleaseAll sends UP for each and clears state. Focus loss, pause, editing, profile changes outside edit mode, geometry changes and normal disconnection release or reset the appropriate gesture state. Cleanup also clears mouse deltas and pending taps so stale motion does not enter a new aim session.

Normal Disconnect closes the TCP client, stops the app's mirror and control processes, removes its own allocated ADB forward and remote JAR. A probe must follow the same ownership discipline; never remove all forwards or terminate every ADB session as routine test cleanup.

## Performance interpretation

An 8 ms gesture tick is a target cadence of 125 Hz, not a measured end-to-end latency or game polling rate. Zero added video buffering still leaves capture, encoding, USB, decode, display and game latency. FPS comes from delivered mirror frames. Both frame-rate and touch response should be observed; healthy video does not prove the game accepted an input.

## Driving mode and native callback update - 11 October 2026

In 1.0.3, `DrivingModeMonitor` polls at a 50 ms target cadence on a background task. `MirrorFrameCapture` reads the foreground mirror's letterboxed video rectangle using a reusable top-down 32-bit GDI DIB. It includes the almost-transparent aiming overlay and discards captures across focus/geometry changes; minimized, portrait, undersized or off-screen views use the phone fallback. It flushes GDI before copying BGRA bytes and restores/deletes its native objects on resize and shutdown. This follows Microsoft's [screen capture](https://learn.microsoft.com/en-us/windows/win32/gdi/capturing-an-image) and [DIB synchronization](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createdibsection) requirements.

Bounded, serialized ADB PNG captures remain a slower background fallback, with one second between completed requests. They run independently so an in-flight screenshot cannot stall fast mirror polling; a healthy current mirror frame takes precedence over a completed fallback. Shutdown cancels/drains the fallback before device cleanup. `DrivingHudDetector` compares four normalized 24 x 24 arrow regions by luminance correlation (threshold 0.72, at least three matches). Entry uses the first positive observation; exit requires two negatives. The original 1.0.1 two-frame entry and 1.5-second capture delay are superseded. Capture/decode errors preserve the mode. Portrait, blank and nonvehicle images do not match. This reads no game memory and does no capture work in the 8 ms input tick. Poll cadence is not a measured end-to-end latency.

Driving uses `Profile.Driving.Bindings` with independent logical action fingers 10+i; no walking joystick is injected. Camera remains logical finger 1 with its existing renewal protocol. Transitions release all fingers and suppress held keys before adopting the next layout. Android allocation and the committed control JAR are unchanged. See [controls.md](controls.md) and [hud.md](hud.md) for evidence and limitations.

The overlay roots its managed owner for the complete native registration lifetime and unregisters its unique class after destruction. This prevents callbacks into a collected delegate, including after reconnect. The regression exercises forced GC, native painting and disposal in a published executable without a phone.

## Wheel input and selected action keys - 11 October 2026

**Implemented:** Raw Input's vertical `RI_MOUSE_WHEEL` flag reads signed `usButtonData` separately from button edges and camera deltas. Wheel pulse identifiers are 0x100/0x101, outside the Windows virtual-key range, and are never passed to asynchronous key-state polling. `MouseWheelInput` accumulates signed deltas in 120-unit notches and preserves every full notch rather than using the button edge sets or 32 ms key deduplication.

Action `Key` strings may contain comma-separated inputs. Walking/driving validation rejects repeated inputs across or within actions. Multiple held inputs share the same logical action finger 10+i. Wheel taps use that same action's edited position and finger, run through the existing tap lifetime, and wait a released tick before the next notch. They do not renew or release camera/fire/scope fingers. Reset clears queued notches and fractional deltas. The Android protocol, allocator and committed JAR are unchanged.
