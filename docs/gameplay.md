# Mapping a touch interface

## Reference layout

PhoneControl translates foreground keyboard and mouse inputs into independent Android touches. The supplied layout is a starting point for a landscape interface, with movement at the lower left, camera dragging in the middle right and editable action buttons. See the root README for the supported-games list.

The positions are normalized coordinates, not universal button locations. Weapon, vehicle, context, aspect ratio and custom HUD settings can change them. Calibrate your own screen through the mapping overlay; keep saved profiles separate from starter defaults.

## Movement, aiming and actions

Historical live observations established movement through one held joystick touch, forward sprint by extending that same touch, and autorun through a separate button tap. A separate camera finger could move while fire or scope remained held. Repeated swipes require renewal of the camera finger without releasing other action fingers.

Tap and Hold describe the duration of the PC-generated touch. The application being controlled may apply its own toggle/hold settings. Map contextual actions only when their buttons are visible. Do not infer successful input from healthy mirrored video alone.

## Idle overlays and performance

An idle touch-protection overlay can dim the phone, reject touches and reduce delivered video FPS while USB streaming continues. Dismiss it on the handset before assessing mappings. A high stream FPS cap cannot increase the underlying application's render rate.

The stream counter reports delivered frames. Input-to-display latency, eligibility for particular game modes and recognition across other HUDs remain unmeasured. Private screenshots are excluded; the public atlas is schematic and automated image inputs are synthetic.
