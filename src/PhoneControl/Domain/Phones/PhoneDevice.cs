// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only





namespace PhoneControl.Domain.Phones;

public sealed record PhoneDevice(string Id, PhoneDeviceState State, string Description, PhoneTransport Transport);
