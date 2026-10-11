# Lessons learned

## Failure history and final remedies

| Symptom | Finding | Remedy / lesson |
|---|---|---|
| USB device visible, no ADB phone | Windows USB enumeration and ADB authorization are different states | Unlock, verify debugging, reconnect, accept the handset prompt; check `adb devices -l` |
| Left Alt needed for aiming | Original capture key was F1 | Normalize Left Alt explicitly; keep Right Alt/F1 available for bindings |
| Cursor still visible while aiming | scrcpy owns its own surface/cursor behavior | Use a no-activate aiming overlay that handles WM_SETCURSOR without stealing mirror keyboard focus |
| Alt+Q closed the phone view | scrcpy's modifier shortcuts collided with game inputs | Move scrcpy shortcut modifier to rsuper; preserve mouse closing |
| Shift caused autorun rather than held sprint | The mapped HUD button was an autorun toggle | Extend the existing joystick finger for sprint; use = for the separate toggle |
| Camera stopped with held fire/scope | Motion source and simultaneous touch ownership needed isolation | Raw Input deltas, independent logical fingers, establish camera before actions |
| First swipe worked, renewed swipe failed with another finger held | Immediate Android local-ID reuse correlated with rejected renewal | Rotate available local IDs while preserving every held finger's ID |
| Long sweeps eventually disabled new buttons too, then recovered | The initial 0–31 allocator entered IDs the game ignored | Keep every Android local ID in 0–9; test many wraps and a newly pressed action |
| Video alive, gestures ignored, FPS near 15 | Samsung idle protection blocked game touches | Inspect screenshot and ask for handset unlock; do not mistake this for transport failure |
| Probe opened an unexpected menu page | Probe began with a menu already open | Observe the starting HUD before injecting; a correct coordinate can have a different meaning in another screen |
| UI automation could not test hidden-cursor aim | Mouse hit testing belonged to PhoneControl's overlay, not the scrcpy process | Use direct isolated transport probes and request a physical mouse gameplay check |
| UI helper could not reconnect | Helper reported “foreground window did not report a process id” | App was installed and tested; user clicked Connect and confirmed the final fix |

## The touch-ID mistake in detail

The first custom allocator used Android's wider 0–31 ID space. It fixed immediate camera restart failure in short tests, which made it look successful. Longer user testing exposed that aiming and newly pressed buttons stopped together and recovered later.

A fresh control-server probe held scope and advanced camera gestures through local IDs. The scoped scene moved at ID 9; the same gesture did not move the scene at ID 10. A new action with ID 13 did not open the backpack. This directly supported a compatibility boundary rather than an assumed USB backlog.

The corrected allocator rotates only available slots 0–9. A live test kept scope down through 128 camera restarts, verified rotation at the final restart, then opened the backpack immediately with a fresh action. The user tested the installed build and replied “it worked!” The allocator regression test now asserts the local-ID range for renewed cameras and actions, not just uniqueness.

The observed cutoff does not prove the game's internal data structure. Record the behavior and the tested workaround without presenting an uninspected engine implementation as fact.

## General rules for future changes

- Success at the operating-system API is not proof of acceptance by the game. Android-valid IDs still failed at the application level.
- Preserve identity for every held gesture, but allocate a fresh compatible identity for a restarted gesture when available.
- Keep logical identity, Android local identity and array index separate in code, logging and tests.
- Test beyond one full allocation cycle. The regression performs 128 renewals and creates a new action on every iteration.
- Test an action first pressed after the stress case. Already-held fingers can remain usable while new fingers fail.
- Recenter the cursor for usability; obtain aim movement from Raw Input rather than cursor-position differences.
- Separate a toggle control from an analog gesture. “Sprint” and “autorun” were visually/functionally distinct despite similar running results.
- Preserve edited profiles during upgrades. A source starter is not the user's saved truth.
- Verify the correct screen and awake state before a probe, and its expected result afterward. The initial corrected-server probe began in a menu; it was repeated in actual gameplay before claiming the scoped test passed.
- Keep video and control experiments isolated. A temporary server and forward should not interrupt the app's renderer or another device session.
- Do not turn an unverified theory into extra infrastructure. Transport flooding was considered, but the local-ID cutoff provided direct evidence and a narrower fix.
- Record delivered FPS and clearly label latency as unmeasured. The 120 FPS cap did not make the observed 60 FPS game run faster.

## Troubleshooting order

1. Capture a fresh phone frame. Check idle protection, the expected gameplay screen, weapon, menu state, ammo and contextual prompts.
2. Confirm the mirror is foreground, aiming is enabled for mouse actions, and pause/edit modes are off.
3. Inspect the saved profile and coordinate convention. Check for a user-edited key, mode or reach rather than assuming defaults.
4. Check DOWN/UP tracing, geometry and the logical finger that owns the gesture.
5. For restart failures, instrument Android local IDs and test repeated renewals plus a new action; compare 0–9 with the failing build only in an isolated probe.
6. Inspect stream/control disconnect errors and USB authorization if the session itself failed.
7. Apply a narrow correction, run relevant allocator/transport checks, republish the complete folder, reconnect and verify actual gameplay.

## Tempting fixes that were insufficient

Moving the camera to the first MotionEvent array index did not solve renewal. Merely resetting the camera coordinates without changing local identity was insufficient with a held action. Rotating through every Android-valid ID passed short tests but broke long tests. Repeated ADB shell swipes would discard the persistent multitouch model rather than repair it.

A physical mouse check remains valuable after transport tests: it covers focus, capture, raw input, overlay and the player's actual gesture timing together. This is how the final fix was accepted.

## Installed dashboard crash - 11 October 2026

**Observed:** Windows Application/.NET event 1025 records `A callback was made on a garbage collected delegate` for `MappingOverlay+WndProc`, followed by FailFast/coreclr exception 0x80131623. This signature was recorded on 3 and 11 October. The dashboard/input process dies while the independent scrcpy renderer survives, explaining video without keybinds.

**Implemented remedy:** USER32 holds a function pointer, not a managed GC root. Each overlay explicitly roots its owner while the native window/class exist, registers a unique class per instance, destroys the window and unregisters its class before releasing the root. Reusing a fixed class name retained the first instance's callback across reconnects; merely keeping the new instance's delegate field was insufficient. Disposal is idempotent; failed native cleanup retains the root to avoid dangling pointers. Raw Input initialization removes its subclass if device registration fails.

The published regression creates 12 overlays through weak references, forces full GC before native painting, disposes each twice and verifies collection afterward. This covers callback lifetime, class reuse and root cleanup. Long physical gameplay remains a separate acceptance check; ordinary exception logging cannot repair this FailFast.
