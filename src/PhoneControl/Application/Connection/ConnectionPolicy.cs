// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only



using PhoneControl.Domain.Phones;

namespace PhoneControl.Application.Connection;

internal static class ConnectionPolicy
{
    // Transport identity is the adapter's responsibility, not an ID naming rule.
    internal static bool CanAutoConnect(IReadOnlyList<PhoneDevice> devices) =>
        devices.Count == 1 && devices[0].State == PhoneDeviceState.Ready && devices[0].Transport == PhoneTransport.Usb;
}
