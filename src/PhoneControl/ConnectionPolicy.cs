// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

namespace PhoneControl;

internal static class ConnectionPolicy
{
    // Avoid guessing between devices or auto-selecting an emulator/network ADB.
    internal static bool CanAutoConnect(IReadOnlyList<DeviceInfo> devices) =>
        devices.Count == 1 && devices[0].State == "device" &&
        !devices[0].Serial.Contains(':') && !devices[0].Serial.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase);
}
