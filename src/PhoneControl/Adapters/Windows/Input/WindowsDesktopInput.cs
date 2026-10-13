// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using PhoneControl.Adapters.Windows.Interop;
using PhoneControl.Application.Ports;
using PhoneControl.Domain.Geometry;

namespace PhoneControl.Adapters.Windows.Input;

internal sealed class WindowsDesktopInput : IDesktopInput
{
    public nint ForegroundWindow => Native.GetForegroundWindow();
    public bool IsWindow(nint window) => Native.IsWindow(window);
    public bool IsMinimized(nint window) => Native.IsIconic(window);
    public bool IsDown(int key) => Native.Down(key);
    public ScreenPoint CursorPosition {
        get { Native.GetCursorPos(out var point); return new(point.X, point.Y); }
    }
    public ScreenRect VideoRect(nint window, int textureWidth, int textureHeight) {
        var rect = Native.VideoRect(window, textureWidth, textureHeight);
        return new(rect.Left, rect.Top, rect.Width, rect.Height);
    }
    public void SetCursorPosition(int x, int y) => Native.SetCursorPos(x, y);
    public void ClipCursor(ScreenRect rectangle) {
        var native = new Native.Rect { Left = rectangle.Left, Top = rectangle.Top, Right = rectangle.Right, Bottom = rectangle.Bottom };
        Native.ClipCursor(ref native);
    }
    public void ReleaseCursor() => Native.UnclipCursor(0);
    public void BeginHighResolutionTimer() => Native.timeBeginPeriod(1);
    public void EndHighResolutionTimer() => Native.timeEndPeriod(1);
}
