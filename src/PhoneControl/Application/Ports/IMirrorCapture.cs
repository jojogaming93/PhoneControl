// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using PhoneControl.Domain.Geometry;

namespace PhoneControl.Application.Ports;

public interface IMirrorCapture : IDisposable
{
    HudFrame? TryCapture(IMirrorSurface surface);
}
