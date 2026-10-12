// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

namespace PhoneControl;

internal static class PhonePortsTest
{
    internal static void Run(Action<bool, string> check)
    {
        var phones = AndroidPhoneBackend.ParseDevices("List of devices attached\n" +
            "alpha device usb:1 model:Example_A\r\n" +
            "beta unauthorized usb:2 model:Example_B\n" +
            "gamma offline\n127.0.0.1:5555 device model:Network\n" +
            "emulator-5554 device\nadb-example._adb-tls-connect._tcp device\n");
        check(phones.Count == 6 && phones[0].State == PhoneDeviceState.Ready && phones[0].Transport == PhoneTransport.Usb &&
            phones[1].State == PhoneDeviceState.Unauthorized && phones[2].State == PhoneDeviceState.Offline &&
            phones[3].Transport == PhoneTransport.Network && phones[4].Transport == PhoneTransport.Emulator &&
            phones[5].Transport == PhoneTransport.Network,
            "Android discovery normalizes brands, authorization and USB/network/emulator transports");
        check(!ConnectionPolicy.CanAutoConnect([phones[5]]), "Wireless discovery names without a colon never auto-connect");
        check(DeviceSession.ParseDisplaySize("Physical size: 1440x3200\nOverride size: 1080x2400") == (1080, 2400) &&
            DeviceSession.ParseDisplaySize("Physical size: 1280x720") == (1280, 720),
            "Android geometry uses the reported effective size across display shapes");
        foreach (string invalid in new[] { "", "unknown", "0x1080", "70000x1080", "999999999999x1080" }) {
            bool rejected = false;
            try { DeviceSession.ParseDisplaySize(invalid); } catch (IOException) { rejected = true; }
            check(rejected, "Invalid display size is rejected without a handset fallback: " + invalid);
        }

        // An alternate backend drives the real gesture engine with no ADB,
        // ToolPaths, control socket, phone or vendor driver dependency.
        IPhoneBackend backend = new TestBackend();
        var device = backend.DiscoverDevices().GetAwaiter().GetResult().Single();
        check(ConnectionPolicy.CanAutoConnect([device]), "Auto-connect uses declared USB transport even with a colon in an opaque backend ID");
        using IPhoneSession session = backend.CreateSession();
        session.Connect(device, new(60, 1280, 8, false)).GetAwaiter().GetResult();
        var recorded = (RecordingSession)session;
        check(session.Connected && recorded.Options == new PhoneConnectionOptions(60, 1280, 8, false),
            "Alternate backend receives stream options without application settings or profiles");
        var profile = Profile.Starter();
        // Keyboard bindings exercise ownership without fabricating foreground
        // mouse capture; mouse bindings are intentionally gated by aim mode.
        profile.Bindings[0].Key = "F1";
        profile.Bindings[1].Key = "F2";
        profile.Bindings[1].Mode = "Hold";
        profile.Validate();
        using var input = new InputEngine(session, profile);
        session.GeometryChanged += input.Reset;
        input.UpdateActions([0x70, 0x71], [0x70, 0x71], [], 0);
        var aim = new AimGesture(session); aim.Begin(profile);
        for (int i = 0; i < 128; i++) aim.Move(profile, 1000, 0, 1280, 720);
        input.UpdateActions([0x70, 0x71, 32], [32], [], 1);
        check(recorded.Active.ContainsKey(10) && recorded.Active.ContainsKey(11) && recorded.Active.ContainsKey(1) && recorded.Active.ContainsKey(12) &&
            recorded.Events.Where(e => e.Pointer is 10 or 11).All(e => e.Action == 0),
            "Alternate input adapter preserves held fire/scope through sustained camera renewals and a new action afterward");
        var movement = InputEngine.MovementPoint(profile, 1, -1, session.Width, session.Height, true);
        check(Math.Abs((movement.X - profile.JoystickX) * session.Width + (movement.Y - profile.JoystickY) * session.Height) < .001,
            "Movement uses alternate phone dimensions to maintain a circular joystick");
        recorded.Resize(720, 1280);
        check(recorded.Active.Count == 0 && session.Width == 720 && session.Height == 1280,
            "Alternate adapter geometry changes reset real input ownership");
        input.SetDetectedDriving(true);
        input.UpdateActions([87], [87], [], 2);
        check(input.Driving && recorded.Active.ContainsKey(10), "Driving bindings run through an alternate input adapter");
        input.SetDetectedDriving(false);
        check(recorded.Active.Count == 0, "Layout changes release alternate adapter fingers");
        session.Back();
        check(recorded.BackCount == 1, "Phone navigation port exposes Back without an Android keycode");
        session.Touch(0, 0, .5, .5);
        session.Disconnect().GetAwaiter().GetResult();
        check(!session.Connected && recorded.Active.Count == 0, "Alternate session disconnection releases held touches");
    }

    private sealed class TestBackend : IPhoneBackend
    {
        public Task<IReadOnlyList<PhoneDevice>> DiscoverDevices() => Task.FromResult<IReadOnlyList<PhoneDevice>>(
            [new("test:usb", PhoneDeviceState.Ready, "Alternate adapter", PhoneTransport.Usb)]);
        public IPhoneSession CreateSession() => new RecordingSession();
    }

    internal sealed class RecordingSession : IPhoneSession
    {
        public Dictionary<long, (double X, double Y)> Active { get; } = new();
        public List<(long Pointer, byte Action)> Events { get; } = new();
        public PhoneConnectionOptions? Options { get; private set; }
        public int BackCount { get; private set; }
        public int Width { get; private set; } = 1280;
        public int Height { get; private set; } = 720;
        public int TextureWidth => Width;
        public int TextureHeight => Height;
        public nint MirrorWindow => 0;
        public bool Connected { get; private set; }
        public bool MirrorAlive => false;
        public event Action? GeometryChanged;
        public event Action<string>? Statistics { add { } remove { } }
        public event Action<string>? Fault { add { } remove { } }
        public Task Connect(PhoneDevice device, PhoneConnectionOptions options) { Options = options; Connected = true; return Task.CompletedTask; }
        public Task Disconnect() { ReleaseAll(); Connected = false; return Task.CompletedTask; }
        public Task<byte[]> CaptureFrame(CancellationToken cancellation) => Task.FromException<byte[]>(new NotSupportedException("This test session has no capture source."));
        public void Touch(long pointer, byte action, double x, double y) {
            Events.Add((pointer, action));
            if (action == 1) Active.Remove(pointer); else Active[pointer] = (x, y);
        }
        public void Back() => BackCount++;
        public void ReleaseAll() { foreach (var (id, pos) in Active.ToArray()) Touch(id, 1, pos.X, pos.Y); }
        public void Resize(int width, int height) { ReleaseAll(); Width = width; Height = height; GeometryChanged?.Invoke(); }
        public void Dispose() => ReleaseAll();
    }
}
