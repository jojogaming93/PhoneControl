# PhoneControl control server

The video/audio renderer still uses the official, unmodified scrcpy 4.1 server.
The separate control channel uses `assets/phonecontrol-control.jar`, rebuilt from
the scrcpy 4.1 sources with the single `PointersState.java` replacement here.
Upstream source: https://github.com/Genymobile/scrcpy/tree/v4.1
License: `LICENSE.scrcpy` (Apache-2.0).

Restarting a camera swipe used to issue UP followed by DOWN with the same Android
local touch ID in one frame. the tested game stopped accepting the new swipe when another
finger stayed down. Cycling through available game-compatible slots 0..9 gives
the new swipe a distinct ID, while retaining each still-held finger's ID and the
ten-finger cap. Android also accepts IDs 10..31, but a live the tested game probe showed
those IDs were ignored by the game. Keeping the rotation inside 0..9 prevents
the periodic loss of both aiming and newly pressed actions.
The PC protocol and logical pointer IDs remain scrcpy 4.1 compatible.

Rebuild with `scripts/Build-ControlServer.ps1`. It requires JDK 21 or newer and
Android SDK platform 36 or newer with build-tools 36.0.0. It verifies the upstream
source archive SHA-256, compiles the server, runs the allocator regression test,
and replaces the prebuilt asset. Running or rebuilding the Windows app uses the
included JAR and does not require Java or an Android SDK.
