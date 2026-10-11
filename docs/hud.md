# HUD atlas and profiles

## Coordinate conventions

All coordinates below are normalized fractions of the visible phone image: top-left is `(0,0)`, bottom-right is `(1,1)`. They are not Windows desktop coordinates or fractions of a title bar/window border. This capture was 2340 × 1080 physical landscape pixels. The rendered texture was 1920 × 886 after the maximum-edge setting.

`DeviceSession.Touch` converts normalized coordinates to physical injection pixels with `round(x * (W - 1))` and `round(y * (H - 1))`. The packet carries the physical W/H, not the texture dimensions. Windows pointer coordinates first have to be converted relative to the actual letterboxed video rectangle.

Use `Native.VideoRect` when mapping desktop clicks. Preserve the image aspect ratio in a documentation overlay. A 1080 × 2340 natural display becomes 2340 × 1080 in landscape; geometry changes release touches before adopting the new dimensions.

## Action positions

| Action | X | Y | Physical pixel at 2340 × 1080 | Starter key / mode | Saved key / mode |
|---|---:|---:|---|---|---|
| Fire / melee | 0.880 | 0.760 | 2058, 820 | MouseLeft / Hold | MouseLeft / Hold |
| Scope / aim down sights | 0.968 | 0.530 | 2264, 572 | MouseRight / Tap | MouseRight / Hold |
| Jump | 0.967 | 0.707 | 2262, 763 | Space / Hold | Space / Hold |
| Crouch | 0.875 | 0.935 | 2047, 1009 | C / Tap | Ctrl / Tap |
| Dodge / roll | 0.954 | 0.907 | 2231, 979 | Ctrl / Tap | C / Tap |
| Reload | 0.809 | 0.935 | 1892, 1009 | R / Tap | R / Tap |
| Contextual interact | 0.647 | 0.826 | 1513, 891 | E / Tap | E / Tap |
| Autorun / sprint lock | 0.744 | 0.307 | 1740, 331 | = / Tap | = / Tap |
| Swap weapon | 0.910 | 0.531 | 2128, 573 | Q / Tap | A / Tap |
| Backpack | 0.023 | 0.700 | 54, 755 | B / Tap | B / Tap |
| Map | 0.937 | 0.129 | 2192, 139 | M / Tap | M / Tap |

These are accepted mapping points based on the tested rifle HUD, not a claim that every point is the visual center of an icon. Fire, scope, autorun and backpack have direct live observations. Interact is provisional/contextual. Other actions are mapped but have less isolated evidence. See the website atlas for per-control evidence and the corresponding image.

## Movement and look anchors

| Parameter | Starter | Saved at documentation time |
|---|---|---|
| Movement keys | W / A / S / D | Z / Q / S / D |
| Joystick anchor | x 0.165, y 0.755 | same |
| Normal joystick radius | 0.09 × screen height | same |
| Sprint key | Shift | Shift |
| Sprint reach | 0.18 × screen height | 0.30 × screen height |
| Look anchor | x 0.61, y 0.48 | same |
| Mouse sensitivity | 1.2 | 1.2 |

The joystick radius uses screen height so movement remains physically circular on a wide screen. At 2340 × 1080, the normal radius is 97.2 pixels, the original tested sprint reach 194.4 pixels, and the later saved reach 324 pixels.

For straight forward movement, normal target Y is `0.755 - 0.09 = 0.665`; original sprint target Y is `0.575`; saved sprint target Y is `0.455`. X stays 0.165. The exact sprint threshold was not measured. The 30% saved setting must not be reported as the original verified reach.

For a direction `(dx,dy)`, normalize by `sqrt(dx²+dy²)`. Horizontal normalized reach is `reach * H/W`; vertical reach is `reach`. For forward diagonals, Shift extends the normalized direction too. Backward or pure sideways movement does not receive the sprint reach.

## Profile files

[starter.json](profiles/starter.json) matches `Profile.Starter()` at documentation time. [example.json](profiles/example.json) is an anonymous alternate key-layout example. These files contain a Profile object, not the full Settings wrapper, so they can be used with the app's profile importer. Import deliberately; it replaces the selected mappings. The website's profile selector only changes the documentation display.

Never overwrite an existing player's profile just because the starter differs. The legacy sprint migration only changes a binding named Sprint with Shift/Hold into Autorun with =/Tap, keeps its HUD position and other bindings, and runs once.

## Calibrating another HUD

1. Obtain a fresh full landscape frame in normal gameplay with the expected weapon equipped. Record W/H and whether it has letterboxing.
2. Release aiming with Esc. Enter F8 mapping edit mode or use Pick on the Controls page.
3. Locate each real action, especially contextual interact. Use normalized image coordinates rather than copying desktop pixels.
4. Check normal joystick motion, forward sprint and return to walking using the same held finger.
5. Test aim alone, aim with scope/fire held, repeated swipe restarts, and a new action afterward.
6. Save an exported profile with a clear device/HUD label and record what was actually tested.

A mode change, custom HUD, expanded menu or different weapon can invalidate a positional assumption. Start each probe with a screenshot; during investigation an already-open menu changed the meaning of otherwise correct coordinates.

## Driving HUD - 11 October 2026

**Observed:** the parked car on the test handset has separate directional buttons rather than the walking joystick. Coordinates use the same 2340 x 1080 landscape image. Keys below are new driving defaults, not changes to historical walking profile snapshots.

| Driving action | X | Y | Initial key (starter / saved) | Behavior | Evidence |
|---|---|---|---|---|---|
| Accelerate | 0.177 | 0.506 | W / Z | Hold | Visible arrow; live motion response |
| Reverse | 0.177 | 0.741 | S / S | Hold | Visible arrow; mapped |
| Steer left | 0.745 | 0.679 | A / Q | Hold | Visible arrow; live heading change |
| Steer right | 0.882 | 0.679 | D / D | Hold | Visible arrow; mapped |
| Boost | 0.087 | 0.518 | Shift / Shift | Hold | Double arrow; function assumed |
| Brake | 0.087 | 0.703 | Space / Space | Hold | Pedal icon; effect not isolated |
| Exit vehicle | 0.972 | 0.500 | E / E | Tap | Labeled Exit; mapped |
| Horn | 0.972 | 0.644 | H / H | Hold | Labeled Horn; mapped |
| Headlights | 0.972 | 0.790 | L / L | Tap | Labeled Headlights; mapped |

Backpack and map retain their initial keys/positions. Driving settings are stored under `Profile.Driving.Bindings`, separately editable and exported with the profile. Four arrow templates live in `assets/reference-driving-hud.json`, embedded in the app. In 1.0.3, at least three matching regions in the first positive frame enable Driving; two absent observations restore On foot. The original 1.0.1 required two positive captures. Other vehicles/HUD layouts may need manual mode and calibration.
