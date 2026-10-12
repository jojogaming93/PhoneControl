// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Runtime.InteropServices;

namespace PhoneControl;

internal readonly record struct HudFrame(byte[] Pixels, int Width, int Height);

// Read the already-rendered video, without another phone capture/PNG round trip.
// The desktop source is valid only while the mirror is foreground and on screen.
internal sealed class MirrorFrameCapture : IDisposable
{
    private nint memoryDc, bitmap, previousBitmap, bits;
    private int width, height;
    private byte[] pixels = [];

    public HudFrame? TryCapture(IMirrorSurface session)
    {
        nint window = session.MirrorWindow;
        if (window == 0 || !Native.IsWindow(window) || Native.IsIconic(window) ||
            Native.GetForegroundWindow() != window) return null;
        var rect = Native.VideoRect(window, session.TextureWidth, session.TextureHeight);
        var frame = TryCapture(rect);
        // Discard captures taken across focus, resize, or orientation changes.
        var after = Native.VideoRect(window, session.TextureWidth, session.TextureHeight);
        if (Native.GetForegroundWindow() != window || Native.IsIconic(window) ||
            rect.Left != after.Left || rect.Top != after.Top || rect.Right != after.Right || rect.Bottom != after.Bottom) return null;
        return frame;
    }

    internal HudFrame? TryCapture(Native.Rect rect)
    {
        int left = Native.GetSystemMetrics(76), top = Native.GetSystemMetrics(77);
        if (rect.Width < 400 || rect.Width <= rect.Height || rect.Height < 1 ||
            rect.Left < left || rect.Top < top || rect.Right > left + Native.GetSystemMetrics(78) ||
            rect.Bottom > top + Native.GetSystemMetrics(79) || (long)rect.Width * rect.Height > 16_000_000) return null;
        nint screen = GetDC(0);
        if (screen == 0) return null;
        try {
            if (bitmap == 0 || width != rect.Width || height != rect.Height) {
                Dispose();
                memoryDc = CreateCompatibleDC(screen);
                if (memoryDc == 0) return null;
                var info = new BitmapInfo { Size = 40, Width = rect.Width, Height = -rect.Height, Planes = 1, BitCount = 32 };
                bitmap = CreateDIBSection(screen, ref info, 0, out bits, 0, 0);
                if (bitmap == 0) { Dispose(); return null; }
                previousBitmap = SelectObject(memoryDc, bitmap);
                if (previousBitmap == 0 || previousBitmap == -1) { previousBitmap = 0; Dispose(); return null; }
                width = rect.Width; height = rect.Height;
                pixels = new byte[checked(width * height * 4)];
            }
            // Include the almost-transparent aiming overlay. No PrintWindow:
            // scrcpy renders through D3D, so its window DC is not a video frame.
            if (!BitBlt(memoryDc, 0, 0, width, height, screen, rect.Left, rect.Top, 0x40CC0020) || !GdiFlush()) return null;
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            return new(pixels, width, height);
        } finally { ReleaseDC(0, screen); }
    }

    public void Dispose()
    {
        if (previousBitmap != 0) SelectObject(memoryDc, previousBitmap);
        if (bitmap != 0) DeleteObject(bitmap);
        if (memoryDc != 0) DeleteDC(memoryDc);
        memoryDc = bitmap = previousBitmap = bits = 0;
        width = height = 0; pixels = [];
    }

    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, SizeImage;
        public int XPelsPerMeter, YPelsPerMeter;
        public uint ColorsUsed, ColorsImportant, Color;
    }
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint value);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(nint dc, int x, int y, int width, int height, nint source, int sx, int sy, uint operation);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
}
