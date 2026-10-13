// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only





namespace PhoneControl.Application.Ports;

public interface IUsbDriverSupport
{
    string DisplayName { get; }
    bool HasMissingDriver();
    Task Install(CancellationToken cancellation);
}
