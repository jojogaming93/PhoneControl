# Installation and distribution

## Release branches and automated pipelines

**Implemented:** development uses `feature/*` branches and pull requests into `main`. A release is stabilized on `release/<major.minor.patch>`, with fixes committed there and merged back into `main`. Each release-branch push runs the installer pipeline and creates or refreshes a draft under `v<major.minor.patch>`. Published versions are immutable; subsequent development uses a new release branch/version.

After these workflow files are merged into `main`, choose **Actions → Start release → Run workflow**, select `main`, and enter an unused version such as `1.0.9`. It creates `release/1.0.9` from the selected main commit and directly invokes the reusable installer workflow. This direct call matters because branch pushes made by the built-in Actions token do not trigger another push workflow. Branch creation refuses an existing branch; rerun **Build Windows installer and release** on that release branch instead. Ordinary maintainer pushes to the branch trigger builds automatically. The current default avoids the existing `1.0.8` draft; always check versions in GitHub Releases before starting another release.

To start a branch using Git instead, use a checkout of the public repository:

```powershell
gh auth switch --hostname github.com --user jojogaming93
git fetch origin main
git switch -c release/1.0.9 origin/main
git push --set-upstream origin release/1.0.9
```

`ci.yml` validates public source, reproducible docs/fixtures and release automation, publishes the Windows app and runs isolated iPhone protocol checks plus the full Android/iPhone regression suite on Windows. Source generation uses Windows and pinned Python 3.14.3: zlib builds on other platforms can produce different compressed PNG bytes for the same fixture pixels, while the audit requires exact fixture bytes. It runs on main, feature, codex and release branch pushes, all pull requests, and manual dispatches. Fork pull requests receive read-only permissions and cannot create releases.

`release.yml` resolves the version from the branch or tag; `windows-installer.yml` is the shared packaging pipeline. It builds the obfuscated app, runs its regression suite, builds the EXE installer, and tests install, repair, uninstall and profile retention before a separate job can upload a release. Any required failure blocks that upload. Diagnostic Actions artifacts include staging self-test results even if installer compilation was never reached. Local automation checks use mocked Git/GitHub commands and do not mutate remote resources.

Draft assets include `PhoneControl-Setup-<version>-win-x64.exe`, its `.sha256`, `PhoneControl-<version>-source.zip` and the build manifest. Each artifact is tied to the exact checked-out source commit. Release builds for one version are serialized; a superseded branch build skips uploading. Draft updates refresh both assets and the target commit. Review the tested draft in GitHub Releases, then publish it there; publication is a maintainer action. Merge stabilization fixes back into main after release.

Version tags such as `1.0.8` and `v1.0.8` remain supported, as does manual **Build Windows installer and release**. New branch/manual drafts use canonical `v` tags. A manual version must match a selected release branch. Existing remote tags cannot move to a different commit, so postpone tagging until stabilization is complete. An already published version, including its alternative bare/`v` tag spelling, is never overwritten. A draft with a different spelling must be reused via that tag or replaced by a new version.

`scripts/New-GitHubRelease.ps1` checks the source commit, clean source tree, version and installer/source hashes before uploading. The build jobs use `contents: read`; `contents: write` is limited to the branch-creation and release-upload jobs. No additional release token or certificate is required.

**Unknown until an Actions run:** hosted Windows installer lifecycle and desktop-capture acceptance. The existing local full-suite failure at `Native mirror capture classifies driving.png at 640px` remains a failing check; pipelines do not bypass it. iPhone protocol checks use the local mock and do not establish real iPhone acceptance. See [verification evidence](verification.md).

Primary automation references: [GitHub workflow syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax), [reusable workflows](https://docs.github.com/en/actions/how-tos/reuse-automations/reuse-workflows), and [triggering workflows with the built-in token](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow).

## Update 1.0.7 - PhoneControl name

**Implemented:** the product, managed assembly/namespace, source folder, launch script, app/installer names, window titles, documentation and assets now use PhoneControl. The app loads `tools/phonecontrol-control.jar`; only the prebuilt asset filename changed, and its bytes/protocol/allocator are unchanged.

Settings and logs now use `%LOCALAPPDATA%\PhoneControl`. On first launch, when new settings are absent, the stable installer registration supplies the previous installation directory's name so the app can copy its saved settings without hard-coding an obsolete product name. The copy preserves the source file and does not overwrite existing PhoneControl settings. Current settings remain distinct from source starter defaults. The installer app ID remains stable so upgrades retain one registration; Windows may retain the previous installation directory. Fresh installs default to `%LOCALAPPDATA%\Programs\PhoneControl`.

The current release version is 1.0.7. Existing installed/running builds are not replaced by a repository rename.

## Update 1.0.6 - branding

**Implemented:** the app, dashboard, desktop shortcuts and both installer executables use the original PhoneControl pilot mascot. Its transparent PNG and multi-resolution Windows icon are in `assets/branding`; the corresponding source archive includes both assets and their packaging script. These are generated artwork, not gameplay captures.

## Update 1.0.5 - 11 October 2026

**Implemented:** PhoneControl now uses the standard **GNU AGPL version 3 only** (`AGPL-3.0-only`). Copyright ownership, installer publisher and app/installer metadata identify **jojogaming93**, linked to `https://github.com/jojogaming93`. Public source and support use `https://github.com/jojogaming93/PhoneControl`.

The sidebar and **About & license** show the running version and copyright. About explains use/modification/redistribution under the AGPL, includes the no-warranty notice, and opens the full license, third-party notices and **corresponding editable source**. `Publish.ps1` bundles `source/PhoneControl-source.zip` and `SOURCE.md` in both portable and installed releases. The installer builder emits the identical ZIP as `PhoneControl-<version>-source.zip` beside the installer, and CI uploads it with the binary. The ZIP contains current readable sources/build scripts/assets and a per-file hash manifest, including uncommitted edits/new source files. Recipient source access needs no GitHub account or network connection.

Obfuscation remains a binary build step; it does not withhold source or restrict recipients' AGPL rights. Public interfaces, native contracts and user mappings are unchanged. Third-party license notices remain intact.

## Update 1.0.4 - 11 October 2026

**Historical implementation:** 1.0.4 introduced copyright/publisher metadata, the running version in the sidebar and **About & license**, and buttons for the license/third-party notices. Its temporary proprietary terms were superseded by the user's AGPLv3 choice in 1.0.5. Editing and sharing one's own mapping profiles remained allowed. Third-party components retained their existing permissions.

The release DLL is obfuscated before packaging, and its protected executable runs the regression suite before installer compilation. This makes private names/strings harder to inspect but cannot prevent decompilation. Public/serialization/WinUI/native-layout contracts are preserved; scrcpy, the Android server and other dependencies retain their original binaries/licenses. Debug symbols and private obfuscation mappings are not shipped.

## Update 1.0.1 - 11 October 2026

**Implemented:** release scripts now default to 1.0.1. Installer tool entries carry SHA-256 values generated from the published payload. Byte-identical installed scrcpy/ADB files are retained, allowing an app update while the shared ADB server has its executable/DLLs open. Missing/changed/corrupt tools still use normal replacement; a changed tool held open may still require closing its owner. The installer does not stop the shared ADB server or delete unrelated forwards.

**Observed on this PC:** the first upgrade attempt hit an in-use `adb.exe`; the corrected installer completed successfully while retaining the identical tools. All 568 installed payload files matched the 1.0.1 release manifest. Saved walking settings were semantically identical; the added driving layout inherited ZQSD. Installed self-tests passed all 86 checks. The app connected to the test handset and its live dashboard/log reported Driving in Auto mode, with delivered frame samples around 51-60 FPS. The driving editor displayed separate accelerator/reverse/steering controls. This is local upgrade/connection evidence, not clean-PC or prolonged gameplay acceptance.

The disposable-account lifecycle suite now checks that the tool hash guard repairs an altered scrcpy license file as well as the control JAR. That full lifecycle suite was not rerun in this account because it intentionally refuses an existing registered installation.

## End-user experience

**Implemented:** open `PhoneControl-Setup-1.0.0-win-x64.exe`. A native, statically linked Win32 launcher extracts the embedded Inno Setup payload and runs its documented `/SILENT /NORESTART` mode. Installation progress is visible, ordinary wizard questions are skipped, and PhoneControl opens afterward. The payload uses per-user installation without elevating the application. Existing running copies can require a close-app prompt; installation errors remain visible.

Default location: `%LOCALAPPDATA%\Programs\PhoneControl`. Desktop and Start-menu shortcuts are created, along with a local guide shortcut and Windows uninstall registration. Re-running the installer repairs/replaces installed files. Settings, edited mappings and logs remain in the separate `%LOCALAPPDATA%\PhoneControl` directory and survive reinstall/uninstall. The installer does not add an autostart entry, background service, account or paid license.

**Bundled:** .NET 10, Windows App SDK 2.5.1 / WinUI 3, scrcpy 4.1, ADB and its USB support libraries, the tested custom control server, runtime notices and the offline field guide. Users do not need Visual Studio, the .NET SDK/runtime, Java, Android SDK, winget, Python, npm or a separate scrcpy installation. Native DLL import inspection on this release found no `msvcp140`/`vcruntime140` runtime dependencies; this must be rechecked after native component updates.

The application installs offline. Driver repair downloads from Samsung and therefore needs an Internet connection if a driver is missing. Windows itself, working USB hardware and a functioning display driver remain prerequisites.

## Supported target and unavoidable actions

This installer targets **Intel/AMD x64 Windows 11, build 26100 (24H2) or newer**. Release testing covered this Windows 11 x64 development PC, not every supported OS build. ARM64 and 32-bit Windows are deliberately rejected. Neither Windows-on-ARM emulation nor Samsung driver compatibility on ARM64 has been validated. The source project's older API floor is not a promise of Windows 10 support for this distribution.

Android requires the owner to enable USB debugging and accept the computer's RSA authorization on the unlocked handset. Installation cannot approve that prompt, dismiss touch protection while the phone is locked, plug in a cable, or turn a charge-only cable into a data cable. On Samsung: Settings → About phone → Software information → tap Build number seven times; enable USB debugging under Developer options; accept **Allow USB debugging?**. Choosing **Always allow from this computer** avoids repeated prompts for that trusted computer.

Windows may require administrator consent/credentials when installing a USB driver. Never document bypassing Windows protection as an installation feature.

## Device onboarding and driver repair

The app polls bundled ADB every two seconds while disconnected, with guards against overlapping discovery/connection/driver operations. An unauthorized phone receives a handset-consent instruction; an offline phone receives a reconnect instruction. When exactly one ready device is present on the Connection page, automatic connection starts once. A serial containing `:` is excluded from automatic connection because it usually denotes a network/emulator endpoint. Multiple devices require choosing a phone; this avoids connecting an arbitrary emulator when several are running.

Automatic connection is enabled by default and can be disabled with its Connection-page toggle. An explicit disconnect suppresses automatic connection for the rest of that app session. A failed automatic connection is not retried continuously; use Refresh devices / Connect or unplug/replug. Returning to Connection permits discovery-driven connection. Saved profiles are loaded before connection and are not replaced by the starter layout. A per-user/session mutex prevents two new PhoneControl copies from running their input controllers simultaneously; opening a second copy brings the existing dashboard forward.

`SamsungDriver.HasMissingDriver()` reads present Windows device nodes using Configuration Manager APIs. Only a Samsung USB hardware ID (`USB\VID_04E8&…`) reporting **problem code 28, missing driver**, and no ADB devices, triggers automatic repair. It is attempted once per app run. An already functioning driver is not replaced automatically. An unrecognized manufacturer, disabled debugging or an unplugged phone does not silently trigger Samsung installation. The user can also request **Repair Samsung USB driver** while disconnected.

Repair downloads the **original Samsung USB Driver for Mobile Phones 1.9.5.0** from Samsung's official site into a unique user-local staging directory. The release's SHA-256 pin is:

```
0ECC9E47D836A7CFF7215EC9CDF33DC7F3E416A7D5EDEDAD522B978B2EB84A20
```

The download is limited to 60 MB and three minutes. A mismatched download is deleted and never executed. The reviewed pinned binary has an NSIS-3 outer installer; repair passes NSIS's case-sensitive `/S` switch and requests elevation for that vendor installer alone. PhoneControl stays unelevated. Exit code 3010 asks for a convenient user restart; no forced restart is issued by PhoneControl. Other errors and declined elevation are shown with a retry option. Cancellation closes an incomplete download; closing PhoneControl does not terminate an already-started elevated driver installation.

**Not yet verified:** the complete Samsung silent-driver flow on a clean Windows PC, including vendor child-process behavior, restart behavior and whether any vendor screens remain. The existing working driver was preserved during development. `/S` is supported by NSIS, but that fact alone does not prove every vendor-specific step is unattended. Do not advertise the driver path as zero-touch until clean-PC acceptance passes. Proprietary Samsung binaries are downloaded at runtime rather than committed or redistributed inside our installer.

## Releasing from a developer machine

```
python docs/build.py
pwsh -File scripts/Build-Installer.ps1 -Version 1.0.6
pwsh -File scripts/Test-Installer.ps1 -Installer artifacts/releases/PhoneControl-Setup-1.0.6-win-x64.exe
```

Build prerequisites belong to maintainers: .NET 10 SDK, Visual Studio C++ x64 tools, Windows SDK and Internet access for missing pinned tools. The prebuilt Android JAR is committed; Java is needed only when changing/rebuilding that server. `Build-Installer.ps1` acquires hash- and publisher-verified Inno Setup 7.1.0 if needed and checks the installed compiler hash. It publishes into a unique staging folder, requires bundled scrcpy 4.1 instead of choosing an arbitrary PATH version, runs application self-tests, checks required runtime/tool/notices files, builds the Inno payload, and embeds it in a statically linked x64 launcher.

The output directory `artifacts/releases` contains the public-facing `PhoneControl-Setup-…exe`, its corresponding `PhoneControl-…-source.zip`, a SHA-256 sidecar, and a JSON manifest with version, source commit/dirty state and per-payload-file hashes. The `PhoneControl-Payload-…exe` backend is a build intermediate; distribute the **Setup** executable with source access. Hashes detect accidental mismatches; download the installer and source from the project's release page.

The **Build Windows installer and release** GitHub Actions workflow builds, lifecycle-tests and attaches these files to a release draft for review. Action revisions are pinned. A hosted CI machine has developer components installed, so it is not proof of clean-PC operation.

## Validation and release gates

**Observed on this development PC:** bundled application self-tests pass; a restricted PATH still runs installed ADB and scrcpy; unattended per-user installation, repair of a deliberately corrupted control JAR, uninstall registration cleanup and exact saved-profile hash preservation passed. No phone gameplay gestures are injected by these installer tests. Interactive default-launch acceptance is tracked in `VALIDATION.md`.

**Follow-up evidence:** all 46 final application checks passed; installed files matched the clean-source release manifest; the test handset connected automatically with the saved ZQSD profile. A historical private GitHub workflow run also passed build/install/repair/uninstall and uploaded its installer. The final 1.0.0 application/installer build corresponds to a historical private source build; this later documentation records its results. These checks do not establish new gameplay FPS or clean-PC driver acceptance.

Before claiming general production readiness, run a clean Windows 11 24H2/25H2 x64 VM/PC with no installed .NET/WinAppSDK/ADB/scrcpy/Visual Studio and test:

1. Default launch from Downloads as a standard user; no component/setup choices, correct shortcuts, successful UI launch and guide opening. Repeat offline, with low disk space and a cancelled install.
2. Authorized phone connection, initially unauthorized connection, unplug/replug, screen lock, disabled debugging, charge-only cable, multiple ADB devices and existing emulator sessions.
3. Missing Samsung driver on real USB hardware, UAC accepted/declined, expired/unreachable download, corrupt payload, vendor exit/restart handling and automatic detection afterward. A VM needs real USB passthrough to prove the driver path.
4. An upgrade from a prior installer version with modified mappings, including a running app, a read-only/in-use file, repair, and uninstall/reinstall. The automated test currently covers same-version reinstall/repair; it does not prove an old-to-new release upgrade.
5. Binary delivery through the intended download channel, checksum verification and Defender/SmartScreen behavior. Refresh bundled self-contained runtimes for security updates: users do not obtain these app-local updates by installing a system runtime.
6. Continued gameplay acceptance: repeated long camera sweeps with held fire/scope, sprint, touch cleanup and actual frame delivery. Packaging tests do not substitute for these game tests.

## Primary references

- [Microsoft self-contained deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps)
- [Current .NET Windows support](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
- [Android device authorization](https://developer.android.com/studio/run/device)
- [Android OEM USB drivers](https://developer.android.com/studio/run/oem-usb)
- [Samsung USB driver download](https://developer.samsung.com/android-usb-driver)
- [Inno privilege modes](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm)
- [Inno command-line modes](https://jrsoftware.org/ishelp/topic_setupcmdline.htm)
- [NSIS silent mode](https://nsis.sourceforge.io/Reference/SilentInstall)
- [Windows missing-driver problem code 28](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/cm-prob-failed-install)
