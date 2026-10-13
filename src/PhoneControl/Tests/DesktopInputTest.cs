// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using PhoneControl.Application.Input;
using PhoneControl.Application.Ports;
using PhoneControl.Domain.Geometry;
using PhoneControl.Domain.Profiles;

namespace PhoneControl.Tests;

internal static class DesktopInputTest
{
    public static void Run(Action<bool, string> check)
    {
        var desktop = new Desktop();
        using var session = new PhonePortsTest.RecordingSession { MirrorWindow = 1 };
        using var engine = new InputEngine(session, Profile.Starter(), desktop, desktop);
        engine.Tick();
        engine.RecordEdge(0xA4, true); engine.Tick();
        check(engine.Aiming && session.Active.ContainsKey(1) && desktop.Clipped,
            "A host port enables aiming and confines the cursor without a native desktop");
        desktop.Keys.Add(1); engine.RecordEdge(1, true); engine.Tick();
        for (int i = 0; i < 128; i++) { engine.RecordMotion(0, 900, -180, 1920, 1080); engine.Tick(); }
        check(session.Active.ContainsKey(10) && session.Events.Where(e => e.Pointer == 10).All(e => e.Action == 0),
            "Host motion through 128 camera renewals preserves a held fire finger");
        desktop.Keys.Add('R'); engine.RecordEdge('R', true); engine.Tick();
        check(session.Active.ContainsKey(15), "A new host action works after sustained camera renewals");
        desktop.ForegroundWindow = 2; engine.Tick();
        check(!engine.Aiming && session.Active.Count == 0 && !desktop.Clipped,
            "Host focus loss releases all held fingers and cursor confinement");
        int events = session.Events.Count;
        engine.RecordEdge('E', true); engine.RecordMotion(0, 900, 900, 1920, 1080); engine.RecordWheel(120);
        desktop.Keys.Clear(); desktop.ForegroundWindow = 1; engine.Tick();
        check(session.Events.Count == events, "Unfocused host input is discarded before focus returns");
        engine.RecordEdge(0xA4, true);
        // The engine intentionally deduplicates the same key for 32 ms.
        Thread.Sleep(40); engine.Tick();
        desktop.Minimized = true; engine.Tick();
        check(session.Active.Count == 0 && !desktop.Clipped && !engine.Aiming,
            "A minimized host mirror releases touches through the desktop port");
    }

    private sealed class Desktop : IDesktopInput, IApplicationLog
    {
        public HashSet<int> Keys { get; } = [];
        public nint ForegroundWindow { get; set; } = 1;
        public bool Minimized { get; set; }
        public bool Clipped { get; private set; }
        public ScreenPoint CursorPosition { get; private set; } = new(740, 410);
        public bool IsWindow(nint window) => window == 1;
        public bool IsMinimized(nint window) => Minimized;
        public bool IsDown(int key) => Keys.Contains(key);
        public ScreenRect VideoRect(nint window, int textureWidth, int textureHeight) => new(100, 50, 1280, 720);
        public void SetCursorPosition(int x, int y) => CursorPosition = new(x, y);
        public void ClipCursor(ScreenRect rectangle) => Clipped = true;
        public void ReleaseCursor() => Clipped = false;
        public void BeginHighResolutionTimer() { }
        public void EndHighResolutionTimer() { }
        public void Write(string message) { }
    }
}
