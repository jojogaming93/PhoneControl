// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PhoneControl;

internal static class DrivingDetectionTest
{
    public static void Run(Func<string, string> fixture, Action<bool, string> check)
    {
        var driving = DrivingModeMonitor.DecodeFrame(File.ReadAllBytes(fixture("driving.png"))).GetAwaiter().GetResult();
        var walking = DrivingModeMonitor.DecodeFrame(File.ReadAllBytes(fixture("walking.png"))).GetAwaiter().GetResult();
        var driveFrame = new HudFrame(driving.Pixels, driving.Width, driving.Height);
        var walkFrame = new HudFrame(walking.Pixels, walking.Width, walking.Height);
        using var session = DeviceSession.ForTransportTest(null!, 2340, 1080);
        using var input = new InputEngine(session, Profile.Starter());
        HudFrame? current = null;
        var gate = new object();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int captures = 0, active = 0, maxActive = 0;
        var monitor = new DrivingModeMonitor(input, () => { lock (gate) return current; }, async cancellation => {
            Interlocked.Increment(ref captures);
            maxActive = Math.Max(maxActive, Interlocked.Increment(ref active));
            started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, cancellation); return walkFrame; }
            finally { Interlocked.Decrement(ref active); }
        });
        try {
            check(started.Task.Wait(2000), "Unavailable mirror starts a bounded background phone capture");
            var timer = Stopwatch.StartNew();
            lock (gate) current = driveFrame;
            check(SpinWait.SpinUntil(() => input.Driving, 500), "Visible vehicle HUD switches while slow phone capture is still pending");
            check(timer.ElapsedMilliseconds < 250, "Walking-to-driving worker response is below 250 ms with a ready frame");
            input.SetEditing(true);
            lock (gate) current = walkFrame;
            Thread.Sleep(150);
            check(input.Driving, "Editing does not replace the active layout through fast detection");
            input.SetEditing(false);
            input.SetModeOverride("Driving");
            Thread.Sleep(150);
            check(input.Driving, "Fast monitor respects the manual driving override");
            input.SetModeOverride("Auto");
            check(SpinWait.SpinUntil(() => !input.Driving, 500), "Two absent observations restore walking through the fast worker");
            lock (gate) current = null;
            Thread.Sleep(150);
            check(captures == 1 && maxActive == 1, "Pending phone screenshots never overlap or stall mirror polling");
        } finally { monitor.Stop().GetAwaiter().GetResult(); }
        check(active == 0, "Stopping the monitor cancels and drains its pending phone capture");

        // A failed background capture must preserve the mode. It must also not
        // prevent a healthy mirror frame from being applied on a later tick.
        input.SetDetectedDriving(true);
        int failures = 0;
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failingMonitor = new DrivingModeMonitor(input, () => { lock (gate) return current; }, cancellation => {
            Interlocked.Increment(ref failures); failed.TrySetResult();
            return Task.FromException<HudFrame>(new IOException("Regression capture failure"));
        });
        try {
            check(failed.Task.Wait(2000), "Fallback failures are observed without stopping the worker");
            Thread.Sleep(150);
            check(input.Driving && failures == 1, "Capture failure retains driving and retries are rate limited");
            lock (gate) current = walkFrame;
            check(SpinWait.SpinUntil(() => !input.Driving, 500), "Mirror detection recovers after a phone screenshot failure");
        } finally { failingMonitor.Stop().GetAwaiter().GetResult(); }

        lock (gate) current = null;
        var oldCapture = new TaskCompletionSource<HudFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var staleMonitor = new DrivingModeMonitor(input, () => { lock (gate) return current; }, cancellation => {
            oldStarted.TrySetResult(); return oldCapture.Task.WaitAsync(cancellation);
        });
        try {
            check(oldStarted.Task.Wait(2000), "Phone fallback can be pending when mirror focus returns");
            lock (gate) current = walkFrame;
            oldCapture.SetResult(driveFrame);
            Thread.Sleep(150);
            check(!input.Driving, "An older driving screenshot cannot overwrite a current walking mirror frame");
        } finally { staleMonitor.Stop().GetAwaiter().GetResult(); }

        lock (gate) current = null;
        var fallbackMonitor = new DrivingModeMonitor(input, () => null, cancellation => Task.FromResult(driveFrame));
        try {
            check(SpinWait.SpinUntil(() => input.Driving, 500), "Completed phone captures still detect driving when the mirror is unavailable");
        } finally { fallbackMonitor.Stop().GetAwaiter().GetResult(); }

        // Exercise the real GDI path with synthetic HUD fixtures rendered in a
        // temporary test surface, never by injecting a touch into the game.
        var previous = Native.GetForegroundWindow();
        nint window = CreateWindowEx(0x08000000, "STATIC", "PhoneControl capture regression", 0x80000000,
            20, 20, 1280, 590, 0, 0, 0, 0);
        if (window == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        try {
            // Consume STARTUPINFO's hidden launch state before explicitly showing
            // this fixture. Otherwise --self-test can capture the desktop beneath
            // an invisible test surface when launched with -WindowStyle Hidden.
            Native.ShowWindow(window, 4); // SW_SHOWNOACTIVATE
            Native.SetWindowPos(window, -1, 20, 20, 1280, 590, 0x0010 | 0x0040);
            using var process = Process.GetCurrentProcess();
            using var capture = new MirrorFrameCapture();
            var classifier = new DrivingHudDetector();
            foreach (var sample in new[] { ("driving.png", true), ("dimmed-driving.png", true), ("walking.png", false), ("blocked.png", false) }) {
                var frame = DrivingModeMonitor.DecodeFrame(File.ReadAllBytes(fixture(sample.Item1))).GetAwaiter().GetResult();
                foreach (int width in new[] { 640, 960, 1280 }.Where(w => w + 40 <= Native.GetSystemMetrics(0) &&
                    w * (double)frame.Height / frame.Width + 40 <= Native.GetSystemMetrics(1))) {
                    int height = (int)Math.Round(width * (double)frame.Height / frame.Width);
                    Native.SetWindowPos(window, -1, 20, 20, width, height, 0x0010 | 0x0040);
                    // Finish the STATIC control's initial/resize paint before
                    // drawing the fixture, so a delayed background paint cannot
                    // erase it just before the real desktop capture.
                    while (PeekMessage(out var message, window, 0, 0, 1)) DispatchMessage(ref message);
                    UpdateWindow(window); DwmFlush();
                    nint dc = GetDC(window);
                    try {
                        var info = new BitmapInfo { Size = 40, Width = frame.Width, Height = -frame.Height, Planes = 1, BitCount = 32 };
                        SetStretchBltMode(dc, 4); // HALFTONE scaling, as in a resized video view.
                        if (StretchDIBits(dc, 0, 0, width, height, 0, 0, frame.Width, frame.Height, frame.Pixels, ref info, 0, 0x00CC0020) == 0)
                            throw new IOException("Could not draw HUD regression fixture.");
                        GdiFlush(); DwmFlush();
                    } finally { ReleaseDC(window, dc); }
                    var read = capture.TryCapture(new Native.Rect { Left = 20, Top = 20, Right = 20 + width, Bottom = 20 + height });
                    check(read is HudFrame pixels && classifier.IsDrivingFrame(pixels.Pixels, pixels.Width, pixels.Height) == sample.Item2,
                        $"Native mirror capture classifies {sample.Item1} at {width}px");
                }
            }
            check(capture.TryCapture(new Native.Rect { Left = -100000, Top = 0, Right = -99000, Bottom = 500 }) is null,
                "Off-screen mirror regions are unavailable rather than classified as walking");
            capture.Dispose(); capture.Dispose();
            // Measure after the codecs/static test window have warmed up, so
            // their lazy GDI objects cannot masquerade as a capture leak.
            uint resources = GetGuiResources(process.Handle, 0);
            for (int i = 0; i < 100; i++) {
                int width = i % 2 == 0 || Native.GetSystemMetrics(0) < 1000 ? 640 : 960;
                capture.TryCapture(new Native.Rect { Left = 20, Top = 20, Right = 20 + width, Bottom = 20 + width / 2 });
                capture.Dispose(); capture.Dispose();
            }
            uint remaining = GetGuiResources(process.Handle, 0);
            check(remaining == resources,
                $"Mirror capture releases all GDI objects after 100 resize/disposal cycles ({resources} to {remaining})");
        } finally { DestroyWindow(window); if (previous != 0) Native.SetForegroundWindow(previous); }
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
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowEx(uint extended, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(nint window);
    [StructLayout(LayoutKind.Sequential)] private struct Message
    {
        public nint Window;
        public uint Id;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public Native.Point Point;
        public uint Private;
    }
    [DllImport("user32.dll", EntryPoint = "PeekMessageW")] private static extern bool PeekMessage(out Message message, nint window, uint minimum, uint maximum, uint remove);
    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")] private static extern nint DispatchMessage(ref Message message);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll")] private static extern int SetStretchBltMode(nint dc, int mode);
    [DllImport("gdi32.dll")] private static extern int StretchDIBits(nint dc, int x, int y, int width, int height, int sx, int sy, int sw, int sh, byte[] pixels, ref BitmapInfo info, uint usage, uint operation);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    [DllImport("user32.dll")] private static extern uint GetGuiResources(nint process, uint flags);
}
