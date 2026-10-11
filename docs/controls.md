# Playing and calibrating PhoneControl

## Connection

Connect your phone with a data-capable USB cable, keep it unlocked and authorize USB debugging. ADB must list it as `device`, not `unauthorized` or `offline`. A visible Samsung USB interface alone did not establish an ADB connection during setup. The successful sequence involved unlocking, toggling USB debugging, reconnecting and accepting the phone's prompt. Do not assume USB-C connectivity means debugging access is available.

Launch the portable app or desktop shortcut, select the phone and click Connect. Click the phone view to give it keyboard focus. The dashboard and video window are separate. Game input is scoped to the foreground phone view, so focusing the dashboard stops held gestures.

## Input modes

| Mode / shortcut | Behavior |
|---|---|
| Normal mouse mode | Left-click/drag injects a touch where the cursor is; right-click sends Android Back |
| Left Alt | Captures/releases aiming; cursor hidden and confined while captured |
| Esc | Releases touches and mouse capture, exits mapping edit mode |
| F8 | Enters/leaves mapping edit mode, releasing active gameplay touches |
| F9 | Pauses/resumes touch controls and releases capture |
| F11 | Fullscreen phone view through scrcpy |
| Switch windows | Releases gameplay fingers, pending motion and cursor confinement |
| Close / Disconnect | Stops the session and removes its own Android/ADB resources |

Left Alt is distinct from Right Alt. The app reserves Left Alt, Esc, F8, F9 and F11. F1 became available for custom mappings after capture moved to Left Alt. The scrcpy modifier was moved to Right Windows (`rsuper`) so Alt+Q no longer quits the connection. The user prefers closing with the mouse.

Mouse action bindings apply while aiming. In normal mode, right-click means Back rather than scope. Keyboard bindings can operate while the phone view is focused outside aiming. Edit and pause modes suppress gameplay gestures.

## Button timing

Hold means a touch remains DOWN while its PC input is held. Tap means one DOWN and an UP about 40 ms later. A press and release occurring between two 8 ms ticks is retained through Raw Input edges; even a very short Hold press receives a short touch instead of disappearing. This is button translation, not an automatic repeated-fire macro.

The starter and saved scope modes differ; see the atlas. A profile Mode controls injected duration, while the game's own scope settings control what the view does with that duration. Verify their interaction in the actual game before changing it.

## Aiming continuously

Raw mouse movement supplies signed deltas independent of held mouse buttons. Mouse sensitivity scales them before conversion to a camera swipe. The OS cursor is recentered and confined, but its position is not used as the aiming-motion source. This matters for large sweeps and simultaneous fire/scope.

The camera has a separate logical finger. When its current swipe reaches the allowed envelope, the controller lifts that finger and starts another swipe at the look anchor. Held fire, scope and movement fingers remain down. The Android server must give the new camera gesture an available local ID in 0–9; merely restarting its coordinates is insufficient.

## Sprint and autorun

Hold Shift with forward movement to drag the existing movement finger farther upward. Release Shift to return that finger to the walking radius; release movement to lift it. Shift alone does not start running. Forward diagonals use the extended reach. Backward and sideways movement retain normal reach.

Use `=` to tap the separate autorun/sprint-lock control. It was observed toggling the HUD indicator on and off. Do not map Shift to that button: it creates the persistent autorun behavior that prompted the user's correction.

The Controls page exposes normal joystick radius, sprint reach, sprint key, look anchor and mouse sensitivity. The user later increased saved sprint reach to 30%, while the original successful live probe used 18%.

## Supported mappings and practical limits

Supported keys are A–Z, 0–9, =/Equals, Space, Shift, Ctrl, RightAlt, Tab, Enter, Backspace, arrows, F1–F7, F10, F12, five mouse buttons and MouseWheelUp/MouseWheelDown. Choose inputs from the Controls dropdowns; movement and sprint use one input each, while an action dropdown allows multiple selections. For example, select A, MouseWheelUp and MouseWheelDown on Swap weapon to retain the keyboard shortcut and use both wheel directions. Profile JSON stores multiple action inputs as comma-separated names in the existing `Key` field. Duplicate movement/action/sprint bindings are rejected, including duplicates among an action's selected inputs. Up to 64 actions can be described in a profile, but only ten fingers may be active at once. Already-held input is suppressed when acquiring focus so switching back does not unexpectedly trigger it.

**Implemented:** wheel actions apply while the phone view is focused and mouse capture is enabled. Each full notch produces a 40 ms tap, even for a Hold action; partial high-resolution deltas accumulate until a notch is complete. Rapid notches queue separately with a released tick between taps. Focus loss, pause, edit, capture and layout changes clear pending wheel input. For Hold actions with several keys selected, releasing one key keeps the action finger down until all selected keys are released. A wheel notch on an already-held action leaves that finger held. These behaviors have local transport regression coverage; physical wheel acceptance in the tested game remains to be confirmed by the player.

Saved settings are at `%LOCALAPPDATA%\PhoneControl\settings.json`; diagnostics are at `%LOCALAPPDATA%\PhoneControl\phonecontrol.log`. Exporting a profile makes a reusable layout. App logs rotate around 4 MB. Touch tracing is optional and records DOWN/UP identities and coordinates, rather than every mouse MOVE.

If touches fail, check focus, capture mode, pause/edit state, contextual HUD changes, ammunition and idle protection before changing the transport. The troubleshooting table in Lessons learned gives the order of investigation.

## Automatic driving layout - 11 October 2026

**Implemented in 1.0.3:** Connection -> Control layout defaults to Auto. A background worker checks the foreground phone mirror every 50 ms and recognizes the example accelerator/reverse/steering arrow shapes. The first frame matching at least three arrows selects Driving; two absent observations restore On foot to tolerate a briefly missing HUD. The original 1.0.1 implementation waited 1.5 seconds between ADB screenshots and required two positives, adding several seconds when entering a vehicle.

When the mirror is unfocused, minimized, too small or partly off screen, serialized ADB screenshots provide a slower fallback, with one second between completed requests and the existing five-second/16 MB limit. A pending phone screenshot never blocks foreground mirror checks, and its result cannot replace a newer mirror frame. Capture errors preserve the mode. Both paths stop with the connection and keep frames in memory. **Unknown:** live enter-vehicle timing has not been measured; 50 ms is the polling target after the HUD reaches the mirror, with video and game latency additional. Keep the mirror visible and unobscured while playing.

Choose On foot or Driving to override recognition if the HUD changes. Controls -> Edit control layout selects independent walking or driving bindings; F8 edits the active layout. Driving uses separate held accelerator/reverse/steering touches. The new layout initially inherits saved movement, sprint, jump and interact keys once, including this player's ZQSD; later driving edits persist in profile import/export. Camera settings are shared. Transitions release all fingers, clear camera motion/taps and suppress held keys until release. Mouse capture stays available in either layout.

On this saved layout: Z accelerates, S reverses, Q/D steer, Shift boosts, Space brakes, E exits, H sounds the horn and L toggles headlights. Secondary actions are mapped from visible icons; only acceleration/left steering received an isolated live response test. **Unknown:** other vehicles/custom HUDs and full driving gameplay. Recognition under power-saving does not dismiss the overlay or unblock game touches.
