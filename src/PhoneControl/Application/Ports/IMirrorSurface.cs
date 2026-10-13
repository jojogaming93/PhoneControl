// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only





namespace PhoneControl.Application.Ports;

// The host remains Windows: mirrors expose a native window for focus/overlays.
public interface IMirrorSurface
{
    nint MirrorWindow { get; }
    int TextureWidth { get; }
    int TextureHeight { get; }
}
