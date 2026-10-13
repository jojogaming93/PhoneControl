// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using PhoneControl.Domain.Profiles;

namespace PhoneControl.Application.Ports;

public interface ISettingsStore
{
    string Directory { get; }
    Settings Load();
    void Save(Settings settings);
}
