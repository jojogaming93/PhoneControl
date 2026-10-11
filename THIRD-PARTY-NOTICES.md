# Third-party components

PhoneControl's original source, documentation and assets: Copyright (c) 2026 [jojogaming93](https://github.com/jojogaming93), licensed under GNU AGPL version 3 only (`AGPL-3.0-only`); see `LICENSE`. Corresponding editable source/build scripts are bundled in `source/PhoneControl-source.zip`; see `SOURCE.md`. Third-party components retain their own copyright/license notices and applicable permissions.

- **scrcpy 4.1**, Genymobile and contributors: Apache License 2.0. https://github.com/Genymobile/scrcpy/tree/v4.1
- **PhoneControl control server** is rebuilt from scrcpy 4.1 with a modified pointer allocator. It cycles Android local touch IDs through game-compatible slots 0..9 instead of immediately reusing a released ID, preserving active IDs and the ten-finger cap. The modified source and Apache-2.0 license are included in tools/control-server-source. The official video/audio client and its server remain unmodified.
- The unmodified official Windows scrcpy distribution includes **ADB**, **SDL**, **FFmpeg**, and **libusb**. Its LICENSE.txt is preserved in tools/scrcpy/LICENSE.txt and covers redistribution notices for those binaries.
- **Microsoft Windows App SDK 2.5.1 / WinUI 3** and **.NET 10**: Microsoft and respective contributors. Preserve the runtime's license and third-party notice files included by dotnet publish.

No proprietary mirroring SDK or paid input-mapping service is used.
