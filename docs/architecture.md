# Phone backends and ports

## Current support

**Implemented:** the Android adapter discovers ADB devices without a manufacturer or model allowlist and obtains the effective display dimensions from Android. Other Android phones can use the same ADB/scrcpy backend when debugging authorization, Windows drivers and Android injection permissions are available. An absent or invalid display size fails connection instead of injecting using the historical test handset's dimensions.

**Unknown:** compatibility and live gameplay acceptance on other handsets have not been verified by this refactor. iPhone support is not implemented. Windows remains the desktop host. Profiles and driving arrow templates describe a HUD, not a universal phone layout; calibrate another HUD using [the atlas procedure](hud.md). Existing saved settings and starter profiles are preserved.

## Dependency direction

The dashboard is the application's entry adapter. It receives `IPhoneBackend` and a list of `IUsbDriverSupport` adapters. `App.OnLaunched` in `Program.cs` is the composition root: it currently supplies `AndroidPhoneBackend` and `SamsungDriver`. Adding another vendor driver does not change dashboard discovery or gameplay gestures.

`InputEngine` depends on `IPhoneInput`; `AimGesture` depends on the narrower `ITouchSink`. `DrivingModeMonitor` receives independent `IMirrorSurface` and `IPhoneFrameSource` ports. Their concrete implementations can be replaced without importing ADB or scrcpy into application input logic. The Windows focus, overlay and raw-input code remains Windows-specific.

| Port in `PhonePorts.cs` | Application responsibility | Current adapter |
|---|---|---|
| `IPhoneBackend` | Discover normalized devices; create an owned session | `AndroidPhoneBackend` |
| `IPhoneSession` | Connection lifecycle, statistics, faults and geometry notifications | `DeviceSession` (Android) |
| `IPhoneInput` / `ITouchSink` | Physical display geometry, normalized touches, release and Back | `DeviceSession` |
| `IMirrorSurface` | Native Windows mirror window and texture dimensions | `DeviceSession`, consumed by `MirrorFrameCapture` |
| `IPhoneFrameSource` | Cancellable encoded PNG capture | `DeviceSession` using bounded ADB screencap |
| `IUsbDriverSupport` | Vendor-specific missing-driver detection and verified installation | `SamsungDriver` |

`PhoneDevice.Id` is opaque to the application. The adapter supplies `PhoneDeviceState` and `PhoneTransport`; `ConnectionPolicy` automatically connects only one ready USB device. Android ID naming rules, including wireless discovery and emulator names, are interpreted inside `AndroidPhoneBackend`. `PhoneConnectionOptions` contains only stream preferences, so adapters do not receive mutable application settings or saved profiles.

The Android adapter owns its ADB serial, processes, remote JAR, allocated forward, TCP stream, active fingers and scrcpy packets. Android Back keycode 4 stays inside it. The JVM local pointer allocator and committed control JAR are unchanged.

## Adding an adapter

1. For another Android vendor with the same transport, use `AndroidPhoneBackend`. If driver repair is needed, implement `IUsbDriverSupport` for that vendor and register it in the composition root. Match only that vendor's missing devices and retain download integrity/elevation checks. The shipped repair adapter remains Samsung-only; other vendors currently use their manufacturer's driver installation.
2. For another transport/platform, implement `IPhoneBackend` and `IPhoneSession` and select that backend in the composition root. Supply authorized/ready status and transport explicitly; do not infer automatic connection from an opaque device ID in the dashboard. This is a source extension point, not a runtime plugin loader or multi-backend selector.
3. Preserve logical touch ownership independently of backend pointer allocation. Touch coordinates are normalized to the visible phone image; release held touches before changing geometry and notify subscribers afterward. Expose Back as navigation, translate it inside the adapter, and retain a Windows mirror window for focus and overlays.
4. Bound PNG capture, honor cancellation and drain its worker before removing device resources. `Disconnect` releases touches and cleans up only this session's resources; the dashboard awaits it before disposal, including connection failure.
5. Run the self-test and relevant backend tests, then validate a calibrated HUD on the actual phone separately. An interface alone cannot supply an operating system's missing remote-input permissions or transport.

## Regression evidence

`PhonePortsTest.cs` drives the actual input/camera controllers through a simulated alternate backend without Android tools or a phone. It covers stream options, opaque IDs, another aspect ratio, 128 camera sweeps with held fire/scope, a new action afterward, geometry resets, driving transitions, Back and disconnect cleanup. Android discovery parsing and effective/invalid display sizes have local checks too.

`DrivingDetectionTest.cs` exercises the production monitor constructor with independent mirror and PNG-source adapters, including cancellation and draining. Existing loopback packet and JVM pointer-allocation tests remain separate from the simulated backend checks. All of these establish implementation behavior; they do not establish another phone's live-game compatibility.
