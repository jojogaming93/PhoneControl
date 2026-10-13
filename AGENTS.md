# PhoneControl project context

This repository is a Windows USB Android mirror and editable touch mapper. Start with [docs/README.md](docs/README.md) for the reusable controls and engineering reference.

Before changing gameplay input, read [the HUD atlas](docs/hud.md), [the control protocol](docs/technical.md) and [lessons learned](docs/lessons-learned.md). Their evidence labels distinguish implementation, live observations, player confirmation and unknowns.

- Keep PC logical pointer IDs, Android local IDs and MotionEvent pointer indices distinct. Logical action IDs may exceed 9; Android local IDs must rotate through available slots 0–9. Held local IDs stay stable.
- Sprint extends the existing joystick finger; = taps the separate autorun control. Preserve the user's edited profile and keep starter defaults distinct from saved settings.
- Raw mouse movement must remain independent of held fire/scope buttons. Repeated camera renewals must not release action fingers.
- Inspect the actual gameplay/idle-overlay state before injecting a probe. Use an isolated server/forward, release all probe fingers and clean up only its own resources.
- Test long sweeps through multiple local-ID wraps and a new action afterward, not just one swipe. Relevant checks are `src/PhoneControl/Tests/SelfTest.cs` and `server/test/PointerStateTest.java`; live game acceptance needs separate evidence.
- Java allocator changes require rebuilding `assets/phonecontrol-control.jar`, publishing, and reconnecting. The prebuilt asset is included in source control; experimental JARs in ignored artifacts are not authoritative.
- The documentation's Markdown chapters and `docs/data/hud.json` are the editable sources. Run `python docs/build.py` after changes to regenerate the offline website. Preserve verified findings and mark new assumptions explicitly.
- Public source must contain no personal screenshots, device identifiers, private paths, credentials or personal contact information. Run `python scripts/Check-PublicSource.py` before packaging. Public fixtures/diagrams are reproduced by `python docs/generate_fixtures.py`; do not substitute private captures. Public documentation focuses on PhoneControl; game names belong in the supported-games list.

Build and maintenance commands are in [docs/maintenance.md](docs/maintenance.md). No game memory manipulation or device-identity changes are part of this implementation. Game mode eligibility and externally measured latency remain unknown.
