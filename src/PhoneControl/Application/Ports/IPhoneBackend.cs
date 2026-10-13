// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only



using PhoneControl.Domain.Phones;

namespace PhoneControl.Application.Ports;

// Ports describe application needs. Wire concrete adapters in Program/App.
public interface IPhoneBackend
{
    Task<IReadOnlyList<PhoneDevice>> DiscoverDevices();
    IPhoneSession CreateSession();
}
