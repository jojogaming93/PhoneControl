# Experimental iPhone connection

## What is implemented

PhoneControl can connect to an already running, dedicated **WebDriverAgent (WDA)** server for an iPhone or an iOS Simulator hosted on a Mac. Select **iPhone · WebDriverAgent (experimental)** on Connection, enter the server address, choose **Use address**, then connect to the discovered endpoint.

The separate Windows viewer displays sequential PNG screenshots. Click to tap, drag to send a straight swipe, hold and release for a long press of up to two seconds, or use **Home**. A gesture is sent after mouse release, as a complete action ending with finger UP. Clicking outside the letterboxed phone image, losing focus/capture, or changing orientation during a pending gesture cancels that gesture locally. One gesture runs at a time. Pointer IDs are not translated into Android pointer IDs.

Screenshot refresh is capped at eight completed captures per second and depends on the server/device. This is an experimental screenshot viewer, not the Android H.264 video/audio stream. Android resolution/bitrate/audio preferences are preserved and apply when selecting Android again. Saved profiles are not replaced by changing the backend.

**Not implemented:** continuous keyboard/mouse gameplay mapping, held joystick/fire/scope fingers, Android Back, audio streaming, automatic Apple device discovery, or installation/startup of WDA. The adapter exposes no `RealtimeInput` port and advertises `SupportsRealtimeTouch=false`; the input engine requires a continuous-input port. iPhone control uses `IPhoneGestureInput` for completed gestures. Home is an iOS button command, not an Android Back substitute.

## Preparing a server

1. Prepare an iPhone or iOS Simulator with compatible Apple developer tooling and start WDA following the [Appium WDA setup guide](https://appium.github.io/appium-xcuitest-driver/latest/guides/wda-custom-server/). Apple development prerequisites are external to the Windows app; no WDA runner or Apple credentials are bundled. Ordinary USB connection alone does not provide these control APIs.
2. Expose the dedicated WDA REST endpoint to the Windows PC. For example, an externally managed USB forward can expose `http://127.0.0.1:8100/`; a Mac-hosted simulator requires a reachable Mac endpoint or tunnel. Enter **WDA's** address, not an Appium server address.
3. Use an instance with no existing WDA session. WDA's create-session route replaces its previous session, so PhoneControl checks reported session ownership and refuses to take over an active session. Keep the instance exclusive while connecting. This preflight is not an atomic lock against another client.
4. Connect manually. The endpoint is always classified as Network, including a loopback USB forward or remotely hosted simulator. It does not trigger the one-USB-phone auto-connect rule or Samsung driver repair.
5. Disconnect before changing the backend or address. PhoneControl cancels/drains its captures and commands, closes its viewer and deletes only the WDA session ID it created. It leaves the external runner, device applications, tunnels and other sessions alone. If a create-session response is lost after WDA accepted it, ownership cannot be proven; close that abandoned session externally before retrying.

Addresses must use HTTP(S), without embedded credentials, query parameters or fragments. The address preference stays in the app's local settings. Keep the unauthenticated WDA service on a trusted private connection; the app does not expose or install that service.

## Geometry and bounded operations

The owned WDA session explicitly sets `screenshotQuality=0` before capture, so the client receives PNG rather than relying on a JPEG/HEIC server default. Wire payloads use explicit JSON keys and are tested after obfuscation.

WDA's `/window/size` reports gesture viewport **points**; PNG dimensions report screenshot **pixels**. The adapter maintains them separately. It checks geometry before and after a capture, validates image dimensions/size and discards captures crossing an orientation change. Commands check current viewport geometry before submission. The viewer maps clicks relative to the displayed image, with no touches in letterboxing.

Each HTTP operation has a five-second timeout; screenshots and JSON responses have size limits. Captures are serialized with control commands. The UI retains at most the current decode and newest pending image. The implementation submits finite gestures with UP in the same request; cancelling a request is not proof that XCTest interrupted a gesture already executing on the phone. There are no persistent held fingers to release through the Android protocol.

## Testing without an iPhone

Apple's simulator runs on macOS. [Appium's non-macOS requirements](https://appium.github.io/appium-xcuitest-driver/latest/guides/non-macos-hosts/) explicitly exclude local simulators on Windows/Linux; installing a Windows tool that resembles a phone would not validate XCTest or iOS behavior.

The app includes a local TCP mock WDA server in its regression suite. It generates account-free grayscale PNGs in memory and tests actual HTTP requests, session ownership, completed touch actions, Home, point/pixel conversion, portrait/landscape changes, stale input rejection, image decoding, cancellation, bounded responses and error handling. It installs nothing on a phone and injects no real touches.

```powershell
dotnet build src/PhoneControl/PhoneControl.csproj -c Release
$app = Resolve-Path src/PhoneControl/bin/Release/net10.0-windows10.0.26100.0/win-x64/PhoneControl.exe
$test = Start-Process $app -ArgumentList --self-test-ios -WindowStyle Hidden -Wait -PassThru
Get-Content (Join-Path (Split-Path $app) self-test-ios-results.txt)
$test.ExitCode
```

These checks also run in the normal `--self-test` suite. The isolated mode separates iPhone protocol verification from Windows desktop-capture fixtures; it does not replace release regression gates.

**Unknown:** real iPhone acceptance, real iOS Simulator acceptance, compatibility across iOS/WDA versions, actual viewer refresh/latency and gameplay acceptance. No iPhone or Mac-hosted simulator was available during implementation. Mock-server success is evidence of client implementation, not evidence of those platforms' behavior.

## Protocol references

- [WDA session and status routes](https://github.com/appium/WebDriverAgent/blob/master/WebDriverAgentLib/Commands/FBSessionCommands.m)
- [WDA viewport and gestures](https://github.com/appium/WebDriverAgent/blob/master/WebDriverAgentLib/Commands/FBElementCommands.m)
- [WDA screenshots](https://github.com/appium/WebDriverAgent/blob/master/WebDriverAgentLib/Commands/FBScreenshotCommands.m)
- [WDA W3C actions](https://github.com/appium/WebDriverAgent/blob/master/WebDriverAgentLib/Commands/FBTouchActionCommands.m)
- [Appium explanation of XCTest input events](https://appium.github.io/appium-xcuitest-driver/latest/guides/input-events/)
