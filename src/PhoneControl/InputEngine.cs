// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;

namespace PhoneControl;

// All game gestures share one persistent socket. No ADB subprocess is started for a key or mouse event.
public sealed class InputEngine : IDisposable
{
    private readonly DeviceSession session;
    private readonly CancellationTokenSource stop = new();
    private readonly object gate = new();
    private Task? loop;
    private Profile profile;
    private readonly HashSet<int> previous = new();
    private readonly HashSet<int> suppressed = new();
    private readonly HashSet<int> rawPressed = new(), rawReleased = new(), rawHeld = new();
    private readonly Dictionary<int, long> lastPress = new();
    private Native.Point? mousePressPoint;
    private readonly Dictionary<long, (double X, double Y, long Due)> taps = new();
    private readonly HashSet<long> heldActions = new();
    private readonly Dictionary<long, long> wheelReady = new();
    private readonly MouseWheelInput wheel = new();
    private readonly Dictionary<int, long> shortMovement = new();
    private readonly MouseMotion motion = new();
    private readonly AimGesture camera;
    private bool wasFocused, joystick, direct;
    private double joyX, joyY, directX, directY;
    private bool editDragging;
    private volatile bool editing, aiming, paused;
    private volatile bool driving, editingDriving;
    private bool detectedDriving;
    private volatile string modeOverride = "Auto";
    public bool Driving => driving;
    public bool DisplayDriving => editing ? editingDriving : driving;
    public string ModeOverride => modeOverride;
    public bool Editing => editing;
    public bool Aiming => aiming;
    public bool Paused => paused;
    public NativeRect Viewport { get; private set; }
    public event Action? StateChanged;
    public event Action<double, double, bool>? EditPoint;
    public InputEngine(DeviceSession session, Profile profile) { this.session = session; this.profile = profile.Copy(); this.profile.EnsureDrivingLayout(); camera = new(session); }
    public void RecordMotion(ushort flags, int x, int y, int desktopWidth, int desktopHeight)
    {
        lock (gate) {
            if (aiming && !editing && !paused && Native.GetForegroundWindow() == session.MirrorWindow)
                motion.Add(flags, x, y, desktopWidth, desktopHeight);
        }
    }
    public void Start() => loop = Task.Run(Run);
    public void RecordWheel(int delta)
    {
        lock (gate) {
            if (aiming && !editing && !paused && session.MirrorWindow != 0 && Native.GetForegroundWindow() == session.MirrorWindow)
                wheel.Add(delta);
        }
    }
    public void RecordEdge(int key, bool down)
    {
        if (Native.GetForegroundWindow() != session.MirrorWindow) return;
        lock (gate) {
            if (!AllKeys().Contains(key)) return;
            if (down && rawHeld.Add(key)) {
                rawPressed.Add(key);
                if (key == 1) { Native.GetCursorPos(out var point); mousePressPoint = point; }
            } else if (!down) { rawHeld.Remove(key); rawReleased.Add(key); }
        }
    }
    public Profile Snapshot() {
        lock (gate) { var snapshot = profile.Copy(); if (DisplayDriving) snapshot.Bindings = snapshot.Driving!.Bindings; return snapshot; }
    }
    public void SetProfile(Profile p) { p.Validate(); lock (gate) { if (!editing) ResetTouches(); profile = p.Copy(); profile.EnsureDrivingLayout(); } }
    public void SetDetectedDriving(bool value)
    {
        lock (gate) { if (editing) return; detectedDriving = value; ApplyDrivingMode(); }
    }
    public void SetModeOverride(string value)
    {
        if (value is not ("Auto" or "On foot" or "Driving")) value = "Auto";
        lock (gate) { modeOverride = value; ApplyDrivingMode(); }
    }
    private void ApplyDrivingMode()
    {
        bool next = modeOverride == "Driving" || modeOverride == "Auto" && detectedDriving;
        if (next == driving) return;
        ResetTouches(); driving = next; previous.Clear(); rawPressed.Clear(); rawReleased.Clear(); rawHeld.Clear();
        suppressed.UnionWith(AllKeys().Where(Native.Down));
        Log.Write("Control layout: " + (driving ? "driving" : "on foot") + " (" + modeOverride + "). Release held keys before using the new layout.");
        StateChanged?.Invoke();
    }
    public void Reset() { lock (gate) { ResetTouches(); suppressed.UnionWith(AllKeys().Where(Native.Down)); } }
    public void SetEditing(bool value, bool? drivingLayout = null)
    {
        lock (gate) { ResetTouches(); editing = value; editingDriving = drivingLayout ?? driving; aiming = false; Native.UnclipCursor(0); previous.Clear(); }
        StateChanged?.Invoke();
    }
    public void SetPaused(bool value)
    {
        lock (gate) { ResetTouches(); paused = value; aiming = false; Native.UnclipCursor(0); suppressed.UnionWith(AllKeys().Where(Native.Down)); }
        StateChanged?.Invoke();
    }
    private IEnumerable<int> AllKeys() => (driving ? profile.Driving!.Bindings : profile.Bindings)
        .SelectMany(b => KeyNames.ParseMany(b.Key))
        .Concat(driving ? [] : new[] { profile.Up, profile.Down, profile.Left, profile.Right, profile.SprintKey }.Select(KeyNames.Parse))
        .Concat([1, 2, 0x1B, 0xA4, 0x77, 0x78]).Distinct();
    private async Task Run()
    {
        Native.timeBeginPeriod(1);
        try {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(8));
            while (await timer.WaitForNextTickAsync(stop.Token)) {
                lock (gate) Tick();
            }
        } catch (OperationCanceledException) { }
        catch (Exception e) { Log.Write("Input loop: " + e); SetPaused(true); }
        finally { lock (gate) ResetTouches(); Native.UnclipCursor(0); Native.timeEndPeriod(1); }
    }
    private void Tick()
    {
        var hwnd = session.MirrorWindow;
        bool focused = hwnd != 0 && Native.IsWindow(hwnd) && !Native.IsIconic(hwnd) && Native.GetForegroundWindow() == hwnd;
        if (!focused) {
            if (wasFocused) { ResetTouches(); aiming = false; Native.UnclipCursor(0); StateChanged?.Invoke(); }
            wasFocused = false; previous.Clear(); rawPressed.Clear(); rawReleased.Clear(); rawHeld.Clear(); motion.Reset(); wheel.Reset(); mousePressPoint = null; return;
        }
        var rect = Native.VideoRect(hwnd, session.TextureWidth, session.TextureHeight);
        Viewport = new(rect.Left, rect.Top, rect.Width, rect.Height);
        if (rect.Width < 20 || rect.Height < 20) return;
        var down = AllKeys().Where(Native.Down).ToHashSet();
        var pressedEdges = rawPressed.ToHashSet(); var releasedEdges = rawReleased.ToHashSet();
        var pressPoint = mousePressPoint;
        rawPressed.Clear(); rawReleased.Clear(); mousePressPoint = null;
        if (!wasFocused) { suppressed.UnionWith(down.Except(pressedEdges)); wasFocused = true; }
        suppressed.RemoveWhere(k => !down.Contains(k));
        down.ExceptWith(suppressed);
        var recognized = new HashSet<int>();
        long edgeTime = Stopwatch.GetTimestamp();
        foreach (int key in down.Concat(pressedEdges).Distinct()) {
            if (suppressed.Contains(key) || previous.Contains(key)) continue;
            // Raw Input and async-state polling may observe the same press on adjacent ticks.
            if (lastPress.TryGetValue(key, out long last) && edgeTime - last < Stopwatch.Frequency * 32 / 1000) continue;
            recognized.Add(key); lastPress[key] = edgeTime;
        }
        bool Pressed(int vk) => recognized.Contains(vk);
        if (Pressed(0x1B)) { ResetTouches(); aiming = false; editing = false; Native.UnclipCursor(0); StateChanged?.Invoke(); }
        if (Pressed(0x77)) { ResetTouches(); editing = !editing; editingDriving = driving; aiming = false; Native.UnclipCursor(0); StateChanged?.Invoke(); }
        if (Pressed(0x78)) { ResetTouches(); paused = !paused; aiming = false; Native.UnclipCursor(0); StateChanged?.Invoke(); }
        if (Pressed(0xA4) && !editing && !paused) {
            ResetTouches(); aiming = !aiming;
            if (aiming) Native.SetCursorPos(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
            else Native.UnclipCursor(0);
            StateChanged?.Invoke();
        }
        Native.GetCursorPos(out var cursor);
        double px = (double)(cursor.X - rect.Left) / rect.Width, py = (double)(cursor.Y - rect.Top) / rect.Height;
        bool inside = px is >= 0 and <= 1 && py is >= 0 and <= 1;
        if (editing) {
            if (Pressed(1) && inside) {
                editDragging = true;
                var start = pressPoint ?? cursor;
                EditPoint?.Invoke(Math.Clamp((double)(start.X - rect.Left) / rect.Width, 0, 1), Math.Clamp((double)(start.Y - rect.Top) / rect.Height, 0, 1), true);
            }
            if (editDragging && (down.Contains(1) || releasedEdges.Contains(1))) EditPoint?.Invoke(Math.Clamp(px, 0, 1), Math.Clamp(py, 0, 1), false);
            if (!down.Contains(1)) editDragging = false;
            previous.Clear(); previous.UnionWith(down); return;
        }
        if (paused) { previous.Clear(); previous.UnionWith(down); return; }
        long now = Stopwatch.GetTimestamp();
        ExpireTaps(now);
        foreach (int key in (driving ? Array.Empty<string>() : new[] { profile.Up, profile.Down, profile.Left, profile.Right }).Select(KeyNames.Parse))
            if (Pressed(key) && !down.Contains(key)) shortMovement[key] = now + Stopwatch.Frequency * 40 / 1000;
        foreach (var pair in shortMovement.ToArray()) if (now >= pair.Value) shortMovement.Remove(pair.Key);
        bool Moving(string key) => down.Contains(KeyNames.Parse(key)) || shortMovement.ContainsKey(KeyNames.Parse(key));
        int dx = driving ? 0 : (Moving(profile.Right) ? 1 : 0) - (Moving(profile.Left) ? 1 : 0);
        int dy = driving ? 0 : (Moving(profile.Down) ? 1 : 0) - (Moving(profile.Up) ? 1 : 0);
        if (dx != 0 || dy != 0) {
            if (!joystick) { session.Touch(0, 0, profile.JoystickX, profile.JoystickY); joystick = true; joyX = profile.JoystickX; joyY = profile.JoystickY; }
            var target = MovementPoint(profile, dx, dy, session.Width, session.Height, down.Contains(KeyNames.Parse(profile.SprintKey)));
            if (Math.Abs(target.X - joyX) > .0001 || Math.Abs(target.Y - joyY) > .0001) { joyX = target.X; joyY = target.Y; session.Touch(0, 2, joyX, joyY); }
        } else if (joystick) { session.Touch(0, 1, joyX, joyY); joystick = false; }
        // Establish the look finger before any new action fingers.
        if (aiming) camera.Begin(profile);
        UpdateActions(down, recognized, releasedEdges, now);
        if (aiming) {
            UpdateWheelActions(wheel, now);
            Native.ClipCursor(ref rect);
            int cx = rect.Left + rect.Width / 2, cy = rect.Top + rect.Height / 2;
            var delta = motion.Drain();
            camera.Move(profile, delta.X, delta.Y, rect.Width, rect.Height);
            if (cursor.X != cx || cursor.Y != cy) Native.SetCursorPos(cx, cy);
        } else {
            if (Pressed(1) && inside) {
                var start = pressPoint ?? cursor;
                direct = true;
                directX = Math.Clamp((double)(start.X - rect.Left) / rect.Width, 0, 1);
                directY = Math.Clamp((double)(start.Y - rect.Top) / rect.Height, 0, 1);
                session.Touch(2, 0, directX, directY);
                if (!down.Contains(1)) {
                    if (Math.Abs(px - directX) > .0001 || Math.Abs(py - directY) > .0001) session.Touch(2, 2, px, py);
                    taps[2] = (px, py, now + Stopwatch.Frequency * 40 / 1000); direct = false;
                }
            }
            if (direct && down.Contains(1) && (Math.Abs(px - directX) > .0001 || Math.Abs(py - directY) > .0001)) {
                directX = Math.Clamp(px, 0, 1); directY = Math.Clamp(py, 0, 1); session.Touch(2, 2, directX, directY);
            }
            if (direct && !down.Contains(1)) { session.Touch(2, 1, directX, directY); direct = false; }
            if (Pressed(2)) session.AndroidKey(4);
        }
        previous.Clear(); previous.UnionWith(down);
    }
    internal void UpdateActions(HashSet<int> down, HashSet<int> pressed, HashSet<int> released, long now)
    {
        var bindings = driving ? profile.Driving!.Bindings : profile.Bindings;
        for (int i = 0; i < bindings.Count; i++) {
            var binding = bindings[i]; long id = 10 + i;
            var keys = KeyNames.ParseMany(binding.Key).Where(k => !KeyNames.IsWheel(k) && (!KeyNames.IsMouse(k) || aiming)).ToArray();
            bool isDown = keys.Any(down.Contains);
            if (binding.Mode == "Hold" && isDown) {
                // Multiple selected keys own a single held finger. A wheel/short
                // tap can become a hold without releasing and restarting it.
                bool wasTap = taps.Remove(id);
                if (heldActions.Add(id) && !wasTap) session.Touch(id, 0, binding.X, binding.Y);
                continue;
            }
            if (heldActions.Remove(id)) session.Touch(id, 1, binding.X, binding.Y);
            if (keys.Any(pressed.Contains)) {
                if (taps.Remove(id, out var oldTap)) session.Touch(id, 1, oldTap.X, oldTap.Y);
                session.Touch(id, 0, binding.X, binding.Y);
                taps[id] = (binding.X, binding.Y, now + Stopwatch.Frequency * 40 / 1000);
            }
        }
    }
    internal void ExpireTaps(long now)
    {
        foreach (var (id, tap) in taps.ToArray()) if (now >= tap.Due) {
            session.Touch(id, 1, tap.X, tap.Y); taps.Remove(id);
            // Give rapid notches a released tick between full 40 ms taps.
            wheelReady[id] = now + Stopwatch.Frequency * 8 / 1000;
        }
    }
    internal void UpdateWheelActions(MouseWheelInput input, long now)
    {
        var bindings = driving ? profile.Driving!.Bindings : profile.Bindings;
        while (input.TryPeek(out int key)) {
            int i = bindings.FindIndex(b => KeyNames.ParseMany(b.Key).Contains(key));
            if (i < 0) { input.Consume(); continue; }
            long id = 10 + i;
            if (heldActions.Contains(id)) { input.Consume(); continue; }
            if (taps.ContainsKey(id) || wheelReady.GetValueOrDefault(id) > now) return;
            input.Consume(); var binding = bindings[i];
            session.Touch(id, 0, binding.X, binding.Y);
            taps[id] = (binding.X, binding.Y, now + Stopwatch.Frequency * 40 / 1000);
        }
    }
    public static (double X, double Y) MovementPoint(Profile p, int dx, int dy, int w, int h, bool sprint = false)
    {
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len == 0) return (p.JoystickX, p.JoystickY);
        // Sprint extends the existing joystick finger; it never taps autorun.
        // It applies only to forward movement. Releasing Shift moves this same
        // finger back to normal reach, and releasing movement lifts it.
        double reach = sprint && dy < 0 ? Math.Max(p.JoystickRadius, p.SprintRadius) : p.JoystickRadius;
        return (Math.Clamp(p.JoystickX + dx / len * reach * h / w, 0, 1), Math.Clamp(p.JoystickY + dy / len * reach, 0, 1));
    }
    private void ResetTouches()
    {
        session.ReleaseAll(); camera.Reset(); motion.Reset(); wheel.Reset(); taps.Clear(); heldActions.Clear(); wheelReady.Clear(); shortMovement.Clear(); joystick = false; direct = false; editDragging = false;
    }
    public void Dispose()
    {
        stop.Cancel();
        try { loop?.Wait(1000); } catch { }
        Native.UnclipCursor(0); stop.Dispose();
    }
}

public readonly record struct NativeRect(int X, int Y, int Width, int Height);
