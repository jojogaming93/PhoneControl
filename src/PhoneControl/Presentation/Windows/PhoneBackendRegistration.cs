// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only



using PhoneControl.Application.Ports;

namespace PhoneControl.Presentation.Windows;

public sealed record PhoneBackendRegistration(string Key, string Label, Func<string, IPhoneBackend> Create,
    IReadOnlyList<IUsbDriverSupport> Drivers, string SetupHelp, bool UsesAddress = false,
    bool SupportsRealtimeTouch = true, Func<string, string>? NormalizeAddress = null);
