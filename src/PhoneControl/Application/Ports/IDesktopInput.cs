// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using PhoneControl.Domain.Geometry;

namespace PhoneControl.Application.Ports;

// Screen coordinates and key state belong to the host, not to the phone transport.
public interface IDesktopInput
{
    nint ForegroundWindow { get; }
    bool IsWindow(nint window);
    bool IsMinimized(nint window);
    bool IsDown(int key);
    ScreenPoint CursorPosition { get; }
    ScreenRect VideoRect(nint window, int textureWidth, int textureHeight);
    void SetCursorPosition(int x, int y);
    void ClipCursor(ScreenRect rectangle);
    void ReleaseCursor();
    void BeginHighResolutionTimer();
    void EndHighResolutionTimer();
}
