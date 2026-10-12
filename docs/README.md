# PhoneControl field guide

This reference covers PhoneControl's USB mirroring, editable controls, transport protocol and engineering lessons. Historical tests used a landscape Android HUD; device identity and private captures are excluded from the public edition. Game support is listed in the root README.

## Start here

Open [index.html](index.html) for the offline website, interactive HUD atlas, searchable chapters and evidence notes. The Markdown files are the source of the written chapters; the website has no runtime dependencies or external requests. After editing the Markdown or HUD data, run `python docs/build.py` from the repository root to regenerate the website.

| File | Purpose |
|---|---|
| [gameplay.md](gameplay.md) | Observed game surfaces, actions, resources and unknowns |
| [hud.md](hud.md) | Exact normalized coordinates, pixel conversion, starter and saved layouts |
| [controls.md](controls.md) | Input modes, sprint, aiming, calibration and profile behavior |
| [technical.md](technical.md) | USB sessions, packet layout, pointer identities, raw input and cleanup |
| [architecture.md](architecture.md) | Phone backend ports, Android/vendor adapters and extension contracts |
| [lessons-learned.md](lessons-learned.md) | Failed approaches, root causes and reusable debugging lessons |
| [verification.md](verification.md) | Evidence, confidence, test coverage and a future test procedure |
| [maintenance.md](maintenance.md) | Source map, build/publish, Android server rebuild and next work |
| [distribution.md](distribution.md) | Self-contained installer, device onboarding, driver repair and release gates |
| [signing-application.md](signing-application.md) | SignPath Foundation application draft and pending approval/integration |

## Agent handoff

- Read `hud.md`, `technical.md` and `lessons-learned.md` before modifying input handling. The distinction between PC logical pointer IDs and Android local pointer IDs is essential.
- Inspect the actual saved profile before interpreting a key or coordinate. The source starter is WASD; this user's saved layout is ZQSD. Right-click is Tap in the starter but Hold in the saved layout.
- Keep Android local IDs within 0–9. Rotate available slots for restarted camera swipes; do not change IDs of fingers that stay held.
- Sprint is an extended joystick gesture. Autorun is a separate HUD tap on `=`. Do not restore the earlier Shift-to-autorun implementation.
- Check the handset for Samsung's idle touch-protection overlay before drawing conclusions from a failed input test.
- Use a separate temporary ADB forward and control server for experiments. Release every finger and remove only the experiment's own resources afterward.
- A few successful swipes are insufficient. Reproduce repeated camera restarts, a held action, and a new action pressed after sweeping.
- The user confirmed the final sustained-sweep fix worked. Keep that verified implementation as the baseline.

## Evidence language

**Observed** means visible in a captured game frame or direct device test. **Implemented** means confirmed in the source, not necessarily in every game situation. **User-confirmed** means the player reported success after installing the corrected build. **Unknown** means not established during this session.

The 0–9 compatibility limit is supported by the observed behavior of this game/device/build. The internal game-engine cause was not instrumented. Full competitive gameplay, mobile-only mode eligibility, every weapon and every game mode were not validated.

## Data and images

Copyright (c) 2026 [jojogaming93](https://github.com/jojogaming93). Original PhoneControl code, documentation and assets use GNU AGPL version 3 only (`AGPL-3.0-only`), with the full text in the repository's `LICENSE`. Use, modification and redistribution are allowed under the AGPL; the software is provided without warranty. Published releases bundle editable source/build scripts in `source/PhoneControl-source.zip`; see the root `SOURCE.md`. Third-party software and game artwork retain their respective notices/licenses/ownership. See `THIRD-PARTY-NOTICES.md` for component notices.

`data/hud.json` contains the atlas coordinates, modes, keys, evidence labels and snapshot metadata. `profiles/starter.json` is importable through PhoneControl's profile importer. `profiles/example.json` provides an anonymous alternate ZQSD layout with 30% sprint reach. The original live sprint test verified 18%; the later 30% value is a saved preference, not a separately measured threshold.

`images/` contains a generated schematic atlas. `fixtures/` contains generated, account-free regression inputs. Run `python docs/generate_fixtures.py` to reproduce both. Original device/game screenshots are kept out of public source, source archives and installers. Historical written findings retain their evidence labels; the synthetic artwork is not live-game evidence.
