# Verification — 3 October 2026

## Update 1.0.1 — 11 October 2026

- Windows events identified the installed crash as USER32 calling a garbage-collected `MappingOverlay+WndProc` delegate (FailFast 0x80131623). The overlay now roots its owner until window destruction/class unregistration, with a unique class per instance and idempotent disposal.
- All **86 published and installed application checks passed**, including 12 forced-GC overlay reconnects/root cleanup, actual driving/walking/idle image fixtures, separate driving fingers, mode cleanup/restoration, manual overrides and saved-profile preservation. Release builds had zero warnings/errors. Fresh JVM compilation passed 128 renewals/new-action presses with local IDs 0–9; the Android JAR is unchanged.
- A short isolated live accelerator/left-steering probe moved/turned the car; a later frame showed it parked at 0.0 km/h. Only these driving responses were isolated. All probe fingers/resources were cleared.
- Installed 1.0.1 connected to the test handset and automatically selected Driving. The dashboard and independent driving editor were inspected. Original walking fields/bindings remained unchanged, including ZQSD, 30% sprint reach and Hold for scope.
- The installer now skips SHA-identical bundled tools, resolving an update blocked by the active shared ADB executable. Existing ADB/forwards were retained. All 568 installed payload files matched the release manifest. The disposable-account lifecycle script gained a corrupt-tool repair regression, but that full suite was not run in the already-installed account.
- Prolonged physical gameplay, other vehicles and custom HUD recognition remain unverified. See the updated offline guide for evidence/coordinates.

- Release build / self-contained publish: successful; zero warnings and errors.
- Deterministic and local TCP transport checks: **40 passed**. Results also appear in the published folder's `self-test-results.txt`.
- Live device: Android test handset, Android, USB ADB authorized.
- Video: official scrcpy 4.1, Direct3D 11 rendering, H.264, 1920×886 capture, 16 Mbps, 120 FPS cap, 0 ms added video buffer.
- Initial the tested game setting: Normal frame rate; approximately 30 delivered FPS.
- Changed the tested game's frame-rate setting to its highest available level (Ultra HD) and confirmed the change. Delivered stream: approximately **60 FPS**, sustained across multiple samples and reconnects.
- The final connected session subsequently entered idle protection / ultra power-saving mode and dropped to approximately 15 FPS. Wake the handset before playing when this overlay appears.
- Direct clicks opened/closed the backpack and navigated game menus.
- Keyboard B opened the backpack; optional touch tracing confirmed exactly one DOWN/UP pair at the correct HUD position.
- Mouse capture (Left Alt), release (Esc), mapping overlay, dashboard navigation, saved profile reload and application restart were exercised.
- Live cursor checks via Windows GetCursorInfo confirmed: cursor hidden while aiming; visible after a second Left Alt press, Esc, or switching to the dashboard. Right Alt did not toggle aiming. The phone view retained keyboard focus while the cursor was hidden.
- Alt+Q was exercised after moving scrcpy's modifier shortcuts away from Alt: the phone window and live stream remained connected.
- Live = presses activated and then deactivated the HUD's autorun/sprint-lock indicator.
- A continuous joystick touch was moved from 9% to 18% reach and back to 9%. The extended gesture activated sprint (the stamina bar depleted), and returning the touch to normal reach ended sprint. Shift now controls this reach on the same movement finger, with no autorun-button touch. Tests cover the transition, releasing movement, backward/sideways behavior, and preserving the user's ZQSD/other bindings when upgrading the old Shift mapping.
- Normal closing removed the app's own ADB forward and temporary Android server JAR.
- A direct live control probe confirmed camera swipes work while the fire finger is held (both touch orders) and while the scope finger is held. The probe released all fingers and removed its own temporary server and forward.
- Camera movement now uses Raw Input motion independently of button presses and starts its own finger before new actions. Tests exercise this camera controller over TCP with both fire and scope held, signed relative/absolute deltas, long swipe renewal without releasing action fingers, and aim cleanup/restart.
- A user check exposed a game-side failure when a restarted swipe immediately reused its Android ID while another action stayed held. The live phone probe reproduced it with the official control server; rotating released IDs allowed subsequent scoped swipes to work.
- Sustained testing then exposed a compatibility limit in the initial 0..31 rotation: the live game accepted camera ID 9 but ignored ID 10, and a new action with ID 13 also failed. The corrected allocator cycles only through slots 0..9, retaining held IDs and the ten-finger cap. A live probe performed 128 consecutive camera restarts with scope held, verified camera rotation on restart 128, and opened the backpack immediately afterward with a new action. Evidence: `artifacts/fixed-sweep-128-before.png`, `artifacts/fixed-sweep-128-after.png`, `artifacts/fixed-action-after-128-sweeps.png`.
- The JVM regression suite verifies 128 camera renewals plus new action presses with fire/scope held, every ID within 0..9, wraparound, uniqueness, capacity and cleanup. The video/audio server remains unmodified.
- After installing the corrected build and reconnecting, the user confirmed the sustained-sweep fix worked. The reconnected stream delivered approximately 53–60 FPS. All 40 published Windows self-tests passed, and the published control JAR matched the tested asset's SHA-256.

Limitations: input-to-display latency has not been measured with external equipment. Simultaneous movement/camera/fire and ten-finger cleanup are verified by transport tests; full competitive gameplay has not been validated. The saved layout is editable, and contextual buttons may require adjustment. Mode eligibility is controlled by the game.

## Windows installer validation — 3 October 2026

- Self-contained WinUI/.NET application and statically linked Win32 installer launcher built successfully. Launcher imports only KERNEL32, USER32, SHELL32 and ole32; no developer runtime is required to start installation.
- **46 application checks passed**, including automatic-connect device selection, preference persistence and rejection of a corrupted Samsung driver download, alongside all 40 existing gameplay/transport checks.
- `scripts/Test-Installer.ps1` passed installation, execution with a restricted PATH, bundled ADB/scrcpy version checks, same-version reinstall repairing an intentionally corrupted control JAR, uninstall registration cleanup, and exact saved-settings hash preservation.
- Default launcher-driven installation completed without wizard clicks, installed per-user, created Desktop/Start-menu shortcuts and automatically started the installed app. Inno logs reported no administrator privileges and no restart. Windows security/download warnings were not exercised by this local development launch.
- Installed WinUI onboarding was inspected visually and through accessibility. Bundled tool paths were selected, no-phone detection stayed responsive without errors, and a second launch exited successfully while retaining a single app process. No phone was attached during this acceptance check; this did not re-validate live gameplay or real device reconnects.
- The official Samsung 1.9.5.0 download's SHA-256 and valid Samsung Authenticode signature were inspected; its NSIS-3 packaging was confirmed. The existing working driver was not replaced. Full unattended driver installation, UAC/restart behavior and vendor child processes remain **unverified on a clean PC**.
- Current installer is **unsigned**, targets Intel/AMD x64 Windows 11 build 26100 or later, and has not yet passed clean-PC/VM or ARM testing. General production readiness, SmartScreen behavior and prior-version upgrade acceptance remain release gates documented in `docs/distribution.md`.
- Follow-up: the final installed build detected and automatically connected an authorized test handset using the saved ZQSD layout. The phone was portrait/idle; this is connection acceptance, not a new high-FPS gameplay measurement. Installed application self-tests passed all 46 checks, and every installed payload file matched the clean-source release manifest for the historical private build.
- A historical private GitHub Actions run passed on `windows-2025`: dependency acquisition, fresh source build, application checks, installer install/repair/uninstall tests and artifact upload. The hosted runner contains developer components; it is not a clean end-user PC or a Samsung USB driver test.
