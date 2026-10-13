// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using PhoneControl.Application.Ports;

namespace PhoneControl.Adapters.Diagnostics;

internal sealed class ApplicationLog : IApplicationLog
{
    public void Write(string message) => Log.Write(message);
}
