// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Runtime.InteropServices;

namespace PhoneControl;

// A small native layered overlay keeps labels out of the video decode/render path.
// In edit mode it passes clicks through. In aim mode a nearly transparent mouse
// surface owns WM_SETCURSOR without taking keyboard focus from the phone view.
// Raw Input still handles buttons; cursor hiding stays local to this window.
public sealed class MappingOverlay : IDisposable
{
    private readonly WndProc callback;
    private readonly nint hwnd;
    private readonly string className = "PhoneControlMappingOverlay_" + Guid.NewGuid().ToString("N");
    private GCHandle lifetime;
    private bool disposed;
    private Profile? profile;
    private bool editing, aiming;
    private bool driving;
    private bool visible;
    private int selected = -1;
    public MappingOverlay()
    {
        callback = Proc;
        var wc = new WindowClass { Size = (uint)Marshal.SizeOf<WindowClass>(), Instance = GetModuleHandle(null),
            ClassName = className, Procedure = Marshal.GetFunctionPointerForDelegate(callback) };
        // USER32 only holds a function pointer, not a managed reference. Root the
        // owner until BOTH the window and its class are gone, including reconnects.
        lifetime = GCHandle.Alloc(this);
        if (RegisterClassEx(ref wc) == 0) { lifetime.Free(); throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); }
        hwnd = CreateWindowEx(0x080800A0, wc.ClassName, "PhoneControl mappings", 0x80000000, 0, 0, 1, 1, 0, 0, wc.Instance, 0);
        if (hwnd == 0) {
            int error = Marshal.GetLastWin32Error();
            UnregisterClass(className, wc.Instance); lifetime.Free();
            throw new System.ComponentModel.Win32Exception(error);
        }
        SetLayeredWindowAttributes(hwnd, 0x010101, 255, 1);
    }
    public void Update(NativeRect rect, Profile p, bool edit, bool aim, int selection, bool visible, bool driving = false)
    {
        bool show = visible && (edit || aim) && rect.Width > 0;
        if (!show) {
            if (this.visible) {
                Native.ShowWindow(hwnd, 0);
                if (aiming) SetCursor(LoadCursor(0, 32512));
            }
            this.visible = false; aiming = false; return;
        }
        if (!this.visible || aiming != aim) {
            // Color-keyed pixels pass hit tests through. Use uniform alpha=1 in
            // aim mode instead, so the whole viewport can hide the OS cursor.
            SetWindowLongPtr(hwnd, -20, aim ? 0x08080080 : 0x080800A0);
            SetLayeredWindowAttributes(hwnd, 0x010101, aim ? (byte)1 : (byte)255, aim ? 2u : 1u);
            if (aiming && !aim) SetCursor(LoadCursor(0, 32512));
        }
        this.visible = true; profile = p; editing = edit; aiming = aim; selected = selection;
        this.driving = driving;
        Native.SetWindowPos(hwnd, -1, rect.X, rect.Y, rect.Width, rect.Height, 0x0010 | 0x0040);
        InvalidateRect(hwnd, 0, false); UpdateWindow(hwnd);
        if (aim) SetCursor(0);
    }
    private nint Proc(nint window, uint msg, nint wp, nint lp)
    {
        if (msg == 0x20 && aiming) { SetCursor(0); return 1; } // WM_SETCURSOR
        if (msg == 0x21) return 3; // MA_NOACTIVATE: keep the phone view foreground.
        if (msg == 0x14) return 1;
        if (msg == 0x0F) {
            nint dc = BeginPaint(window, out var paint);
            try {
                Native.GetClientRect(window, out var r);
                nint bg = CreateSolidBrush(0x010101); FillRect(dc, ref r, bg); DeleteObject(bg);
                SetBkMode(dc, 1);
                nint font = CreateFont(-17, 0, 0, 0, 600, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
                var oldFont = SelectObject(dc, font);
                if (editing && profile is not null) {
                    void Mark(double x, double y, string label, int idx, int radius = 25) {
                        int cx = (int)(x * r.Width), cy = (int)(y * r.Height);
                        uint color = selected == idx ? 0x00DBFFu : 0xFFE469u;
                        nint pen = CreatePen(0, 3, color); nint oldPen = SelectObject(dc, pen);
                        nint oldBrush = SelectObject(dc, GetStockObject(5));
                        Ellipse(dc, cx - radius, cy - radius, cx + radius, cy + radius);
                        SetTextColor(dc, color); TextOut(dc, cx - radius, cy + radius + 4, label, label.Length);
                        SelectObject(dc, oldBrush); SelectObject(dc, oldPen); DeleteObject(pen);
                    }
                    if (!driving) Mark(profile.JoystickX, profile.JoystickY, "MOVE", -1, (int)(profile.JoystickRadius * r.Height));
                    Mark(profile.LookX, profile.LookY, "LOOK", -2);
                    for (int i = 0; i < profile.Bindings.Count; i++) Mark(profile.Bindings[i].X, profile.Bindings[i].Y, profile.Bindings[i].Key, i);
                    string hint = "EDIT • select a marker or click to place • F8 / Esc to finish";
                    SetTextColor(dc, 0xFFE469); TextOut(dc, 18, r.Height - 28, hint, hint.Length);
                }
                SelectObject(dc, oldFont); DeleteObject(font);
            } finally { EndPaint(window, ref paint); }
            return 0;
        }
        return DefWindowProc(window, msg, wp, lp);
    }
    public void Dispose() {
        if (disposed) return;
        disposed = true;
        Native.ShowWindow(hwnd, 0);
        if (aiming) SetCursor(LoadCursor(0, 32512));
        // All calls occur on the creating UI thread. Do not unroot a callback
        // if USER32 still owns a window/class after a failed cleanup.
        if (DestroyWindow(hwnd) && UnregisterClass(className, GetModuleHandle(null))) lifetime.Free();
        GC.KeepAlive(callback);
    }
    private delegate nint WndProc(nint hwnd, uint msg, nint wp, nint lp);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass {
        public uint Size, Style; public nint Procedure; public int ClassExtra, WindowExtra; public nint Instance, Icon, Cursor, Background;
        public string? MenuName; public string ClassName; public nint SmallIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Paint {
        public nint Dc; public int Erase; public Native.Rect Rect; public int Restore, IncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Reserved;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClass cls);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool UnregisterClass(string name, nint instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowEx(uint ex, string cls, string title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint inst, nint param);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(nint h, uint key, byte alpha, uint flags);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint h, int index, nint value);
    [DllImport("user32.dll")] private static extern nint SetCursor(nint cursor);
    [DllImport("user32.dll", EntryPoint = "LoadCursorW")] private static extern nint LoadCursor(nint instance, nint name);
    [DllImport("user32.dll")] private static extern nint BeginPaint(nint h, out Paint p);
    [DllImport("user32.dll")] private static extern bool EndPaint(nint h, ref Paint p);
    [DllImport("user32.dll")] private static extern bool InvalidateRect(nint h, nint r, bool erase);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(nint h);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint h);
    [DllImport("user32.dll")] private static extern nint DefWindowProc(nint h, uint m, nint w, nint l);
    [DllImport("user32.dll")] private static extern int FillRect(nint dc, ref Native.Rect r, nint brush);
    [DllImport("gdi32.dll")] private static extern nint CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern nint CreatePen(int style, int width, uint color);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] private static extern nint GetStockObject(int obj);
    [DllImport("gdi32.dll")] private static extern bool Ellipse(nint dc, int l, int t, int r, int b);
    [DllImport("gdi32.dll")] private static extern int SetBkMode(nint dc, int mode);
    [DllImport("gdi32.dll")] private static extern uint SetTextColor(nint dc, uint color);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern bool TextOut(nint dc, int x, int y, string text, int len);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateFont(int h, int w, int escape, int orient, int weight, uint italic, uint underline, uint strike, uint charset, uint outp, uint clip, uint quality, uint family, string face);
    [DllImport("gdi32.dll")] private static extern bool MoveToEx(nint dc, int x, int y, nint point);
    [DllImport("gdi32.dll")] private static extern bool LineTo(nint dc, int x, int y);
}
