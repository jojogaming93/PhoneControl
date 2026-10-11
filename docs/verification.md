# Evidence and verification

## AGPL release/source checks - 11 October 2026

**Implemented in 1.0.5:** standard AGPLv3-only licensing with `jojogaming93` copyright attribution, source/build instructions and a source ZIP in published/installed releases. **Observed:** the initial 77-file source ZIP's manifest hashes matched every extracted input, a fresh `git init` could repackage that extracted source without a commit, and the extracted C# project built with zero warnings/errors.

**Observed test-fixture issue:** later 1.0.4 and initial 1.0.5 native-capture checks sometimes read the browser below the temporary STATIC window even when Windows reported that window as visible. This affected both ordinary and obfuscated builds. The 1.0.5 fixture explicitly handles hidden process startup, dispatches its pending window messages and finishes initial/resize painting before drawing each sample. Two consecutive ordinary runs then passed all 136 checks. This changes only the self-test window's setup; no gameplay input or phone capture behavior changes. Temporary failure screenshots/diagnostics used to inspect this issue are not release inputs. Installer/installed/live-game checks remain separate evidence.

## Confidence ledger

| Finding | Evidence | Scope |
|---|---|---|
| Backpack B/direct touch works | Visible backpack opening; trace with one DOWN/UP pair | Tested landscape HUD |
| Holding forward + extended joystick sprints | Stamina depleted at 18% reach; return to 9% ended sprint | Live straight-forward test |
| = controls autorun separately | HUD indicator activated and deactivated | Live two presses |
| Aim can move with fire/scope held | Isolated phone probes in both initial touch orders | Tested rifle/scoped views |
| Local ID 10 and later ID 13 ignored | Camera 9 moved; camera 10 did not; action 13 failed | Tested game/device/build |
| Rotation within 0–9 fixes sustained case | 128 scoped restarts, subsequent backpack; user success | Live server + installed app |
| Every ID stays in 0–9, held IDs stable | JVM allocator test | Deterministic source behavior |
| Windows packet/controller checks | 40 published self-tests passed | Local transport and logic |
| Cursor hidden/restored and Left Alt distinct | Live cursor query and UI checks | Tested Windows session |
| 53–60 delivered FPS after correction | scrcpy FPS log samples | Tested graphics/device state |
| Exact latency / all modes / 30% sprint threshold | No measurement | Unknown |

## Public fixtures and historical evidence

Private phone/game captures are excluded from public source. The atlas uses the schematic `images/hud-schematic.svg`, containing only anonymous control positions. Written live observations above and historical test results below remain historical evidence; the public package does not contain the original visual proof.

`generate_fixtures.py` generates four account-free grayscale PNGs in `fixtures/` from the anonymous 24 x 24 arrow grids in `assets/reference-driving-hud.json`. These cover positive, dimmed positive, blank negative and dimmed blank negative inputs. They exercise the real PNG decoder, resizing/GDI capture and monitor timing/cancellation without usernames, chats, inventory or device identifiers. Synthetic fixtures do not independently validate recognition in arbitrary real game scenes. New gameplay acceptance requires separate, consented testing.

## Existing checks

The published Windows executable's `--self-test` produced 40 passes. It covers profile validation/roundtrip, duplicate/reserved keys, Left/Right Alt normalization, the one-time sprint migration, Equals, packet endianness/pressure/dimensions, circular diagonals, walk–sprint–walk identity, raw relative/absolute motion, multitouch cleanup and the actual camera controller over a real loopback TCP connection.

The Android allocator JVM suite runs as part of `Build-ControlServer.ps1`. It keeps fire and scope held while performing 128 camera renewals and new action presses; it asserts compatible ID bounds, held-ID stability, uniqueness, wraparound, the ten-finger cap and cleanup. Its MotionEvent class is a small test double for fields only, not an Android injection/emulator test.

Transport tests cannot by themselves establish game acceptance. The original 0–31 allocator could pass abstract identity tests while the game rejected some IDs. Both layers are needed.

## Safe live regression sequence

1. Confirm an awake, normal gameplay HUD and expected saved profile. Capture the initial frame. Release active touches before starting an isolated probe.
2. Verify a simple known action, such as opening/closing Backpack. Avoid spending currency or altering inventory.
3. Compare walking, forward sprint and returning to walking on the same joystick finger. Observe stamina rather than relying on a similar animation.
4. Capture aiming with no button, with scope held, and with a brief fire press. Avoid prolonged firing that consumes the account's ammunition.
5. Exercise long horizontal/vertical sweeps, both directions, through more than one full local-ID cycle. Keep another action finger held.
6. After the sweep, press an action that was not previously held, then movement/jump. Check immediate response, not delayed recovery.
7. Release capture with Esc, switch windows, pause, edit and disconnect; check that no movement/fire/clip remains stuck.
8. Reconnect using the published JAR and perform a physical 30-second mouse test. Record the result separately from direct injected touch probes.

Experimental connections need unique scids, a unique remote JAR and their own `tcp:0` forward. Send UP for every active logical finger in a finally block. Close only the probe's socket/process, remove only its allocated forward and JAR, and restore the game's starting screen.

## Updating this reference

For a new finding, record date, device/OS, game HUD state, profile values, implementation version, precise trigger, visible result and evidence file. Mark whether it was source-inspected, transport-tested, device-observed or user-confirmed. Preserve a failed reproduction when it helps explain the correction. Do not silently generalize the test handset evidence to another game or client.

## Driving and dashboard crash checks - 11 October 2026

**Installed acceptance:** 1.0.1 upgraded this PC successfully while leaving its existing ADB server running; 568 installed payload files matched the release manifest. Installed self-tests passed all 86 checks. Saved walking fields/bindings remained identical, including ZQSD, 30% sprint reach and held right-click scope. The connected dashboard showed Driving in Auto mode and the editor exposed independently editable driving bindings. No new prolonged gameplay acceptance is claimed.

**Observed:** Release build/publish passed with zero warnings/errors; all 86 application checks passed, including actual PNG classification (driving, dimmed driving, rifle and walking idle-protection), two-frame transitions, independent accelerator/steering fingers, cleanup/override/restoration, profile preservation and 12 forced-GC overlay reconnects with root cleanup. Fresh JVM compilation passed the existing 128 camera renewal/new-action allocator suite. The Android JAR is unchanged.

A short isolated accelerator/left-steering probe changed the car position/heading and showed 14.6 km/h in its following screenshot. A subsequent frame showed 0.0 km/h. Every probe finger, its own server, forward and temporary files were removed. Only accelerator/left steering got isolated live response evidence; braking was applied but its effect was not independently measured. Private historical screenshots described the test handset car HUD. Other vehicles/HUDs and long physical gameplay remain unknown.

## Faster driving detection checks - 11 October 2026

**Observed in local tests for 1.0.3:** the worker response test passed within a 250 ms bound with a ready driving fixture while its phone screenshot remained indefinitely pending. This verifies that fallback capture cannot block mirror recognition; the implementation targets a 50 ms polling cadence. Tests also cover first-positive entry, interrupted exit evidence, manual overrides, editing, capture failure/recovery, fallback serialization/cancellation, stale screenshot precedence, successful background detection and retained input cleanup/sustained-sweep checks.

The real native screen-capture path correctly classified driving, dimmed driving, walking and idle-protection fixtures drawn at 640, 960 and 1280 pixels wide. Resizing and double disposal left the GDI object count unchanged. These tests render a temporary Windows surface and inject no phone touches. **Unknown:** actual enter-vehicle latency, live GPU mirror acceptance and prolonged physical gameplay after this change remain separate checks; the test timing excludes game/video latency.
