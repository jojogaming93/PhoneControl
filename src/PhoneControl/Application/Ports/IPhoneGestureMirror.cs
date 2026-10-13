// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only



using PhoneControl.Domain.Phones;

namespace PhoneControl.Application.Ports;

public interface IPhoneGestureMirror : IDisposable
{
    nint Window { get; }
    bool Alive { get; }
    void Present(PhoneScreenshot frame);
}
