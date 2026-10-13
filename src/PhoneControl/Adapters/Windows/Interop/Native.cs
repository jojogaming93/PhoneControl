// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Runtime.InteropServices;

using PhoneControl.Domain.Profiles;

namespace PhoneControl.Adapters.Windows.Interop;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; public Point(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; public int Width => Right - Left; public int Height => Bottom - Top; }
    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(nint hwnd, ref Point point);
    [DllImport("user32.dll")] public static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(nint hwnd, int cmd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] public static extern bool ClipCursor(ref Rect rect);
    [DllImport("user32.dll", EntryPoint = "ClipCursor")] public static extern bool UnclipCursor(nint rect);
    [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint period);
    public static bool Down(int key) => !KeyNames.IsWheel(key) && (GetAsyncKeyState(key) & 0x8000) != 0;
    public static Rect VideoRect(nint hwnd, int videoWidth, int videoHeight)
    {
        GetClientRect(hwnd, out var r);
        var p = new Point(); ClientToScreen(hwnd, ref p);
        double scale = Math.Min((double)r.Width / Math.Max(1, videoWidth), (double)r.Height / Math.Max(1, videoHeight));
        int w = (int)Math.Round(videoWidth * scale), h = (int)Math.Round(videoHeight * scale);
        int x = p.X + (r.Width - w) / 2, y = p.Y + (r.Height - h) / 2;
        return new Rect { Left = x, Top = y, Right = x + w, Bottom = y + h };
    }
}
