// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Buffers.Binary;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace PhoneControl;

public static class SelfTest
{
    public static int Run()
    {
        var results = new List<string>();
        void Check(bool value, string name) { if (!value) throw new Exception(name); results.Add("PASS " + name); }
        void Reject(Action action, string name) {
            bool rejected = false; try { action(); } catch (InvalidDataException) { rejected = true; } Check(rejected, name);
        }
        try {
            string settingsMigration = Path.Combine(Path.GetTempPath(), "PhoneControl-settings-" + Guid.NewGuid().ToString("N"));
            try {
                string previousDirectory = Path.Combine(settingsMigration, "previous");
                string destinationDirectory = Path.Combine(settingsMigration, "current");
                Directory.CreateDirectory(previousDirectory);
                var editedSettings = new Settings { AutoConnect = false };
                editedSettings.Profile.Name = "Edited layout";
                editedSettings.Profile.Sensitivity = 2.7;
                string editedJson = JsonSerializer.Serialize(editedSettings);
                File.WriteAllText(Path.Combine(previousDirectory, "settings.json"), editedJson);
                Check(Settings.ImportSettingsIfMissing(previousDirectory, destinationDirectory) &&
                    File.ReadAllText(Path.Combine(destinationDirectory, "settings.json")) == editedJson &&
                    File.ReadAllText(Path.Combine(previousDirectory, "settings.json")) == editedJson,
                    "Product rename copies edited settings exactly and preserves the original");
                string currentJson = JsonSerializer.Serialize(new Settings { MaxFps = 90 });
                File.WriteAllText(Path.Combine(destinationDirectory, "settings.json"), currentJson);
                Check(!Settings.ImportSettingsIfMissing(previousDirectory, destinationDirectory) &&
                    File.ReadAllText(Path.Combine(destinationDirectory, "settings.json")) == currentJson,
                    "Product rename never overwrites existing current settings");
                Check(!Settings.ImportSettingsIfMissing(Path.Combine(settingsMigration, "missing"), Path.Combine(settingsMigration, "fresh")),
                    "Fresh installs do not import absent settings");
            } finally { Directory.Delete(settingsMigration, recursive: true); }
            Check(new Settings().AutoConnect, "Fresh settings enable automatic phone connection");
            Check(!JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(new Settings { AutoConnect = false }))!.AutoConnect,
                "Automatic connection preference survives serialization");
            Check(ConnectionPolicy.CanAutoConnect([new("phone", PhoneDeviceState.Ready, "model:TEST_DEVICE", PhoneTransport.Usb)]), "One authorized USB phone can auto-connect");
            Check(!ConnectionPolicy.CanAutoConnect([]) && !ConnectionPolicy.CanAutoConnect([new("phone", PhoneDeviceState.Unauthorized, "", PhoneTransport.Usb)]) &&
                !ConnectionPolicy.CanAutoConnect([new("phone", PhoneDeviceState.Offline, "", PhoneTransport.Usb)]), "Absent, unauthorized and offline phones never auto-connect");
            Check(!ConnectionPolicy.CanAutoConnect([new("phone", PhoneDeviceState.Ready, "", PhoneTransport.Usb), new("another", PhoneDeviceState.Ready, "", PhoneTransport.Usb)]) &&
                !ConnectionPolicy.CanAutoConnect([new("127.0.0.1:5555", PhoneDeviceState.Ready, "", PhoneTransport.Network)]) &&
                !ConnectionPolicy.CanAutoConnect([new("emulator-5554", PhoneDeviceState.Ready, "", PhoneTransport.Emulator)]), "Multiple devices, network ADB and emulators require manual selection");
            PhonePortsTest.Run(Check);
            string corruptDriver = Path.GetTempFileName();
            try {
                File.WriteAllText(corruptDriver, "not an official driver");
                bool rejected = false;
                try { SamsungDriver.VerifyDownload(corruptDriver, CancellationToken.None).GetAwaiter().GetResult(); } catch (IOException) { rejected = true; }
                Check(rejected, "Driver integrity gate rejects altered downloads before execution");
            } finally { File.Delete(corruptDriver); }
            var p = Profile.Starter(); p.Validate(); Check(p.Bindings.Count == 11, "example starter profile is valid with eleven actions");
            var customDriving = p.Copy(); customDriving.Up = "Z"; customDriving.Left = "Q";
            customDriving.Bindings.Single(b => b.Name == "Swap weapon").Key = "A";
            string walkingBefore = JsonSerializer.Serialize(customDriving.Bindings);
            customDriving.EnsureDrivingLayout(); customDriving.Validate();
            Check(customDriving.Driving!.Bindings[0].Key == "Z" && customDriving.Driving.Bindings[2].Key == "Q" &&
                JsonSerializer.Serialize(customDriving.Bindings) == walkingBefore, "Driving defaults inherit ZQSD without replacing walking bindings");
            customDriving.Driving.Bindings[0].X = .2; customDriving.EnsureDrivingLayout();
            var savedDriving = customDriving.Copy(); savedDriving.Validate();
            Check(savedDriving.Driving!.Bindings[0].X == .2 && customDriving.Driving.Bindings[0].X == .2 &&
                JsonSerializer.Serialize(savedDriving.Bindings) == walkingBefore, "Driving edits persist and initialization never resets them");
            var badDriving = customDriving.Copy(); badDriving.Driving!.Bindings[1].Key = "Z";
            Reject(badDriving.Validate, "Duplicate keys are rejected within the driving layout");
            badDriving = customDriving.Copy(); badDriving.Driving!.Bindings[0].X = double.NaN;
            Reject(badDriving.Validate, "Non-finite driving positions rejected");
            var detector = new DrivingHudDetector();
            Check(detector.Observe(true), "First clear vehicle frame immediately enables driving");
            Check(detector.Observe(false) && detector.Observe(true) && detector.Observe(false) && !detector.Observe(false),
                "One missing HUD does not leave driving and positive evidence resets the exit debounce");
            Check(detector.Observe(true), "Driving can immediately re-enter after returning on foot");
            Check(!detector.IsDrivingFrame(new byte[400 * 200 * 4], 400, 200), "Blank captures cannot enable driving mode");
            string Fixture(string name) {
                for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent) {
                    string path = Path.Combine(folder.FullName, "docs", "fixtures", name);
                    if (File.Exists(path)) return path;
                }
                throw new FileNotFoundException("HUD regression fixture missing: " + name);
            }
            foreach (var fixture in new[] { ("driving.png", true), ("dimmed-driving.png", true), ("walking.png", false), ("blocked.png", false) }) {
                var frame = DrivingModeMonitor.DecodeFrame(File.ReadAllBytes(Fixture(fixture.Item1))).GetAwaiter().GetResult();
                Check(detector.IsDrivingFrame(frame.Pixels, frame.Width, frame.Height) == fixture.Item2, "Synthetic HUD classification: " + fixture.Item1);
            }
            for (int reconnect = 0; reconnect < 12; reconnect++) {
                var weak = CreateOverlay();
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                Check(PaintAndDispose(weak), "Native overlay stays rooted through GC and reconnect " + reconnect);
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                Check(!weak.TryGetTarget(out _), "Disposed overlay releases its native lifetime root " + reconnect);
            }
            var copy = p.Copy(); copy.Bindings[0].X = .5; Check(p.Bindings[0].X != copy.Bindings[0].X, "Profile snapshots do not share bindings");
            var roundtrip = JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(p))!; roundtrip.Validate();
            Check(roundtrip.Bindings[0].Key == "MouseLeft", "Export/import preserves mouse bindings");
            var wheelProfile = p.Copy();
            wheelProfile.Bindings.Single(b => b.Name == "Swap weapon").Key = "Q, MouseWheelUp, MouseWheelDown";
            wheelProfile.Validate(); var wheelRoundtrip = wheelProfile.Copy(); wheelRoundtrip.Validate();
            Check(wheelRoundtrip.Bindings[8].Key == "Q, MouseWheelUp, MouseWheelDown" &&
                KeyNames.Parse("mousewheelup") == KeyNames.WheelUp && KeyNames.Parse("MouseWheelDown") == KeyNames.WheelDown,
                "Both wheel directions and the existing weapon key survive profile export/import");
            var wheelInvalid = wheelProfile.Copy(); wheelInvalid.Bindings[0].Key = "MouseWheelUp";
            Reject(wheelInvalid.Validate, "Wheel directions cannot belong to multiple actions in one layout");
            wheelInvalid = wheelProfile.Copy(); wheelInvalid.Bindings[8].Key = "Q, q";
            Reject(wheelInvalid.Validate, "Duplicate aliases within one action are rejected");
            wheelInvalid = wheelProfile.Copy(); wheelInvalid.Up = "MouseWheelUp";
            Reject(wheelInvalid.Validate, "Wheel pulses cannot be assigned as held movement keys");
            Check(KeyNames.Choices.Select(KeyNames.Parse).Distinct().Count() == KeyNames.Choices.Length &&
                !KeyNames.Choices.Any(k => KeyNames.Parse(k) is 0 or 0x1B or 0xA4 or 0x77 or 0x78 or 0x7A) &&
                KeyNames.Canonical("Equals") == "=", "Dropdown choices are supported, unique and exclude reserved shortcuts");
            var wheelInput = new MouseWheelInput();
            wheelInput.Add(60); Check(!wheelInput.TryPeek(out _), "Partial high-resolution wheel motion waits for a full notch");
            wheelInput.Add(60); Check(wheelInput.TryPeek(out int wheelKey) && wheelKey == KeyNames.WheelUp, "Positive signed wheel delta becomes wheel up");
            wheelInput.Consume(); wheelInput.Add(-240);
            Check(wheelInput.TryPeek(out wheelKey) && wheelKey == KeyNames.WheelDown, "Negative multi-notch delta becomes wheel down");
            wheelInput.Consume(); Check(wheelInput.TryPeek(out wheelKey) && wheelKey == KeyNames.WheelDown, "Multiple notches in one raw packet are retained");
            wheelInput.Add(60); wheelInput.Reset(); wheelInput.Add(60);
            Check(!wheelInput.TryPeek(out _), "Reset clears both queued notches and partial wheel motion");
            wheelInput.Reset();
            var invalid = p.Copy(); invalid.Bindings[0].Key = "W"; Reject(invalid.Validate, "Duplicate movement/action keys rejected");
            invalid = p.Copy(); invalid.LookX = double.NaN; Reject(invalid.Validate, "Non-finite positions rejected");
            invalid = p.Copy(); invalid.Bindings[0].Key = "LeftAlt"; Reject(invalid.Validate, "Reserved Left Alt capture key rejected");
            invalid = p.Copy(); invalid.Bindings[0].Key = "Alt"; Reject(invalid.Validate, "Generic Alt alias cannot conflict with capture");
            var remapped = p.Copy(); remapped.Bindings[0].Key = "F1"; remapped.Bindings[1].Key = "RightAlt"; remapped.Validate();
            Check(KeyNames.Parse("F1") == 0x70 && KeyNames.Parse("RightAlt") == 0xA5, "F1 and Right Alt remain available for mappings");
            Check(RawInputReader.NormalizeKey(0x12, 0) == 0xA4 && RawInputReader.NormalizeKey(0x12, 1) == 0xA4 &&
                RawInputReader.NormalizeKey(0x12, 2) == 0xA5 && RawInputReader.NormalizeKey(0x12, 3) == 0xA5 &&
                RawInputReader.NormalizeKey(0xA4, 0) == 0xA4 && RawInputReader.NormalizeKey(0xA5, 0) == 0xA5,
                "Raw Input distinguishes Left and Right Alt on press and release");
            invalid = p.Copy(); invalid.Bindings[0].X = 1.01; Reject(invalid.Validate, "Out-of-screen coordinates rejected");
            Check(KeyNames.Parse("mouseleft") == 1 && KeyNames.Parse("z") == 90 && KeyNames.Parse("unknown") == 0, "Key parsing");
            Check(KeyNames.Parse("=") == 0xBB && KeyNames.Parse("Equals") == 0xBB &&
                p.Bindings.Single(b => b.Name == "Autorun") is { Key: "=", Mode: "Tap" }, "Equals triggers autorun as a separate tap");
            invalid = p.Copy(); invalid.Bindings[0].Key = "Shift"; Reject(invalid.Validate, "Sprint key cannot also tap an action");
            invalid = p.Copy(); invalid.SprintRadius = double.NaN; Reject(invalid.Validate, "Non-finite sprint reach rejected");
            var legacy = p.Copy(); legacy.Up = "Z"; legacy.Left = "Q";
            legacy.Bindings.Single(b => b.Name == "Swap weapon").Key = "A";
            var oldSprint = legacy.Bindings.Single(b => b.Name == "Autorun");
            oldSprint.Name = "Sprint"; oldSprint.Key = "Shift"; oldSprint.Mode = "Hold"; oldSprint.X = .72;
            Check(legacy.UpgradeSprintMapping() && !legacy.UpgradeSprintMapping(), "Sprint mapping upgrade runs once");
            legacy.Validate();
            Check(legacy.Up == "Z" && legacy.Left == "Q" && oldSprint is { Name: "Autorun", Key: "=", Mode: "Tap", X: .72 } &&
                legacy.Bindings.Single(b => b.Name == "Swap weapon").Key == "A", "Upgrade preserves custom movement, HUD position and other keys");
            var b = DeviceSession.Packet(0x0102030405060708L, 0, 1234, 567, 2340, 1080);
            Check(b.Length == 32 && b[0] == 2 && b[1] == 0, "scrcpy 4.1 touch type and packet length");
            Check(BinaryPrimitives.ReadInt64BigEndian(b.AsSpan(2)) == 0x0102030405060708L, "64-bit multitouch pointer ID is big-endian");
            Check(BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(10)) == 1234 && BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(14)) == 567, "Touch positions are big-endian");
            Check(BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(18)) == 2340 && BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(20)) == 1080, "Touch screen size matches physical landscape resolution");
            Check(BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(22)) == 65535 && b.AsSpan(24).IndexOfAnyExcept((byte)0) < 0, "Finger pressure is full and mouse button flags are zero");
            var up = DeviceSession.Packet(10, 1, -20, 5000, 2340, 1080);
            Check(BinaryPrimitives.ReadUInt16BigEndian(up.AsSpan(22)) == 0 && BinaryPrimitives.ReadInt32BigEndian(up.AsSpan(10)) == 0 && BinaryPrimitives.ReadInt32BigEndian(up.AsSpan(14)) == 1079, "Release pressure and edge clamping");
            var horizontal = InputEngine.MovementPoint(p, 1, 0, 2340, 1080); var diagonal = InputEngine.MovementPoint(p, 1, 1, 2340, 1080);
            double radius((double X, double Y) pt) => Math.Sqrt(Math.Pow((pt.X - p.JoystickX) * 2340, 2) + Math.Pow((pt.Y - p.JoystickY) * 1080, 2));
            Check(Math.Abs(radius(horizontal) - radius(diagonal)) < .001, "Diagonal joystick motion has the same physical radius");
            var centered = InputEngine.MovementPoint(p, 0, 0, 2340, 1080);
            Check(centered == (p.JoystickX, p.JoystickY), "Opposing directions return joystick center");
            var normalForward = InputEngine.MovementPoint(p, 0, -1, 2340, 1080);
            var sprintForward = InputEngine.MovementPoint(p, 0, -1, 2340, 1080, true);
            Check(sprintForward.X == p.JoystickX && Math.Abs(sprintForward.Y - (p.JoystickY - p.SprintRadius)) < .0001 &&
                sprintForward.Y < normalForward.Y, "Forward sprint extends joystick upwards");
            Check(InputEngine.MovementPoint(p, 0, 0, 2340, 1080, true) == centered &&
                InputEngine.MovementPoint(p, 0, 1, 2340, 1080, true) == InputEngine.MovementPoint(p, 0, 1, 2340, 1080) &&
                InputEngine.MovementPoint(p, 1, 0, 2340, 1080, true) == horizontal, "Sprint alone, backward and sideways do not force forward running");
            var mouse = new MouseMotion();
            mouse.Add(0, 17, -9, 1920, 1080); mouse.Add(0, -3, 4, 1920, 1080);
            Check(mouse.Drain() == (14d, -5d) && mouse.Drain() == (0d, 0d), "Raw mouse movement accumulates signed deltas and drains once");
            mouse.Add(3, 10000, 20000, 3840, 1080);
            Check(mouse.Drain() == (0d, 0d), "First absolute mouse sample establishes a baseline without a camera jump");
            mouse.Add(3, 11000, 19000, 3840, 1080);
            var absoluteMotion = mouse.Drain();
            Check(Math.Abs(absoluteMotion.X - 1000 * 3839 / 65535d) < .0001 && absoluteMotion.Y < 0,
                "Absolute mouse input converts to signed desktop pixel movement");
            mouse.Add(0, 50, 40, 1920, 1080); mouse.Reset();
            Check(mouse.Drain() == (0d, 0d), "Leaving aim mode discards pending mouse motion");
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            try {
                using var outbound = new TcpClient { NoDelay = true };
                var accept = listener.AcceptTcpClientAsync();
                outbound.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
                using var inbound = accept.GetAwaiter().GetResult(); inbound.ReceiveTimeout = 2000;
                using var transport = DeviceSession.ForTransportTest(outbound.GetStream(), 2340, 1080);
                using (var wheelEngine = new InputEngine(transport, wheelProfile)) {
                    wheelInput.Add(240); wheelInput.Add(-120);
                    long tick = Stopwatch.Frequency * 8 / 1000, tapDuration = Stopwatch.Frequency * 40 / 1000;
                    wheelEngine.UpdateWheelActions(wheelInput, 0);
                    wheelEngine.UpdateWheelActions(wheelInput, tick);
                    var firstNotch = new byte[32]; inbound.GetStream().ReadExactly(firstNotch);
                    Check(firstNotch[1] == 0 && BinaryPrimitives.ReadInt64BigEndian(firstNotch.AsSpan(2)) == 18 && !inbound.GetStream().DataAvailable,
                        "Wheel injection uses the existing weapon finger and queues rapid notches instead of collapsing them");
                    wheelEngine.ExpireTaps(tapDuration - 1);
                    Check(!inbound.GetStream().DataAvailable, "Wheel taps remain down for the complete 40 ms duration");
                    wheelEngine.ExpireTaps(tapDuration); wheelEngine.UpdateWheelActions(wheelInput, tapDuration);
                    var notchUp = new byte[32]; inbound.GetStream().ReadExactly(notchUp);
                    Check(notchUp[1] == 1 && !inbound.GetStream().DataAvailable, "Rapid wheel taps have a released tick between them");
                    long secondStart = tapDuration + tick;
                    wheelEngine.UpdateWheelActions(wheelInput, secondStart); wheelEngine.ExpireTaps(secondStart + tapDuration);
                    long thirdStart = secondStart + tapDuration + tick;
                    wheelEngine.UpdateWheelActions(wheelInput, thirdStart); wheelEngine.ExpireTaps(thirdStart + tapDuration);
                    var nextNotches = new byte[128]; inbound.GetStream().ReadExactly(nextNotches);
                    Check(Enumerable.Range(0, 4).All(i => nextNotches[i * 32 + 1] == i % 2 && BinaryPrimitives.ReadInt64BigEndian(nextNotches.AsSpan(i * 32 + 2)) == 18) &&
                        !wheelInput.TryPeek(out _) && !inbound.GetStream().DataAvailable, "Every up/down notch emits exactly one complete weapon tap");
                    wheelEngine.UpdateActions([81], [81], [], thirdStart + tapDuration + tick);
                    wheelEngine.ExpireTaps(thirdStart + tapDuration * 2 + tick);
                    var keyboardWeapon = new byte[64]; inbound.GetStream().ReadExactly(keyboardWeapon);
                    Check(keyboardWeapon[1] == 0 && keyboardWeapon[33] == 1, "Keyboard weapon switching still works alongside wheel bindings");
                    var holdProfile = wheelProfile.Copy(); holdProfile.Bindings[2].Key = "Space, F2";
                    wheelEngine.SetProfile(holdProfile);
                    wheelEngine.UpdateActions([32], [32], [], 0);
                    wheelEngine.UpdateActions([32, 0x71], [0x71], [], 1);
                    wheelEngine.UpdateActions([0x71], [], [32], 2);
                    var holdDown = new byte[32]; inbound.GetStream().ReadExactly(holdDown);
                    Check(holdDown[1] == 0 && !inbound.GetStream().DataAvailable, "Multiple hold keys share one finger and releasing one key preserves it");
                    wheelEngine.UpdateActions([], [], [0x71], 3);
                    var holdUp = new byte[32]; inbound.GetStream().ReadExactly(holdUp);
                    Check(holdUp[1] == 1, "Releasing the last selected hold key lifts the shared action finger");
                    var sweep = new AimGesture(transport); sweep.Begin(wheelProfile);
                    transport.Touch(10, 0, .88, .76); transport.Touch(11, 0, .968, .53);
                    for (int renew = 0; renew < 128; renew++) sweep.Move(wheelProfile, 1000, 0, 1280, 590);
                    var sweeping = new List<byte[]>();
                    while (inbound.GetStream().DataAvailable) { var packet = new byte[32]; inbound.GetStream().ReadExactly(packet); sweeping.Add(packet); }
                    Check(sweeping.Count(packet => packet[1] == 1) > 128 && sweeping.Skip(3).All(packet => BinaryPrimitives.ReadInt64BigEndian(packet.AsSpan(2)) == 1),
                        "Sustained camera renewals preserve held fire/scope while wheel support is enabled");
                    wheelInput.Add(-120); wheelEngine.UpdateWheelActions(wheelInput, Stopwatch.Frequency);
                    wheelEngine.ExpireTaps(Stopwatch.Frequency + tapDuration);
                    var afterSweep = new byte[64]; inbound.GetStream().ReadExactly(afterSweep);
                    Check(afterSweep[1] == 0 && afterSweep[33] == 1 && BinaryPrimitives.ReadInt64BigEndian(afterSweep.AsSpan(2)) == 18,
                        "A new wheel action works after sustained sweeps without releasing camera, fire or scope");
                    wheelInput.Add(120); wheelEngine.Reset(); sweep.Reset(); wheelInput.Reset();
                    var resetFingers = new byte[96]; inbound.GetStream().ReadExactly(resetFingers);
                    Check(Enumerable.Range(0, 3).All(i => resetFingers[i * 32 + 1] == 1) && !inbound.GetStream().DataAvailable, "Reset releases every remaining finger after wheel and camera stress");
                }
                using (var engine = new InputEngine(transport, customDriving)) {
                    transport.Touch(0, 0, .165, .755); transport.Touch(1, 0, .61, .48);
                    engine.SetDetectedDriving(true);
                    var switched = new byte[128]; inbound.GetStream().ReadExactly(switched);
                    Check(switched[65] == 1 && switched[97] == 1 && engine.Driving &&
                        engine.Snapshot().Bindings[0].Name == "Accelerate", "Entering driving releases walking/camera fingers before replacing actions");
                    engine.UpdateActions([90, 81], [90, 81], [], 0);
                    var driveDown = new byte[64]; inbound.GetStream().ReadExactly(driveDown);
                    engine.UpdateActions([90], [], [81], 0);
                    var steeringUp = new byte[32]; inbound.GetStream().ReadExactly(steeringUp);
                    Check(BinaryPrimitives.ReadInt64BigEndian(driveDown.AsSpan(2)) == 10 &&
                        BinaryPrimitives.ReadInt64BigEndian(driveDown.AsSpan(34)) == 12 &&
                        steeringUp[1] == 1 && BinaryPrimitives.ReadInt64BigEndian(steeringUp.AsSpan(2)) == 12,
                        "Accelerator and steering own independent fingers; releasing steering keeps accelerator held");
                    engine.SetDetectedDriving(false);
                    var driveUp = new byte[32]; inbound.GetStream().ReadExactly(driveUp);
                    Check(driveUp[1] == 1 && BinaryPrimitives.ReadInt64BigEndian(driveUp.AsSpan(2)) == 10 &&
                        !engine.Driving && engine.Snapshot().Bindings[0].Name == "Fire / melee", "Leaving driving releases accelerator and restores walking bindings");
                    engine.SetModeOverride("Driving"); engine.SetDetectedDriving(false);
                    Check(engine.Driving, "Manual driving override ignores absent HUD detection");
                    engine.SetModeOverride("On foot"); engine.SetDetectedDriving(true);
                    Check(!engine.Driving, "Manual walking override ignores vehicle HUD detection");
                    engine.SetModeOverride("Auto");
                    Check(engine.Driving, "Returning to Auto adopts the detected mode");
                }
                transport.Touch(0, 0, .16, .75); transport.Touch(1, 0, .6, .5); transport.Touch(10, 0, .88, .76);
                transport.Touch(1, 2, .65, .5); transport.Touch(10, 1, .88, .76); transport.ReleaseAll();
                var packets = new byte[32 * 7]; inbound.GetStream().ReadExactly(packets);
                Check(packets[1] == 0 && packets[33] == 0 && packets[65] == 0, "Movement, camera and fire can hold three independent fingers");
                Check(BinaryPrimitives.ReadInt64BigEndian(packets.AsSpan(98)) == 1 && packets[97] == 2, "Camera move preserves its own pointer ID");
                Check(packets[129] == 1 && packets[161] == 1 && packets[193] == 1, "ReleaseAll emits UP for every remaining held finger");
                transport.Touch(0, 0, p.JoystickX, p.JoystickY);
                transport.Touch(0, 2, normalForward.X, normalForward.Y);
                transport.Touch(0, 2, sprintForward.X, sprintForward.Y);
                transport.Touch(0, 2, normalForward.X, normalForward.Y);
                transport.Touch(0, 1, normalForward.X, normalForward.Y);
                var sprintPackets = new byte[32 * 5]; inbound.GetStream().ReadExactly(sprintPackets);
                Check(Enumerable.Range(0, 5).All(i => BinaryPrimitives.ReadInt64BigEndian(sprintPackets.AsSpan(i * 32 + 2)) == 0) &&
                    sprintPackets[1] == 0 && sprintPackets[33] == 2 && sprintPackets[65] == 2 && sprintPackets[97] == 2 && sprintPackets[129] == 1 &&
                    BinaryPrimitives.ReadInt32BigEndian(sprintPackets.AsSpan(78)) < BinaryPrimitives.ReadInt32BigEndian(sprintPackets.AsSpan(46)) &&
                    BinaryPrimitives.ReadInt32BigEndian(sprintPackets.AsSpan(110)) == BinaryPrimitives.ReadInt32BigEndian(sprintPackets.AsSpan(46)),
                    "Shift press/release uses the same joystick finger and never taps autorun");
                for (int i = 0; i < 11; i++) transport.Touch(i, 0, .5, .5);
                transport.Touch(99, 2, .5, .5); transport.Touch(99, 1, .5, .5); transport.ReleaseAll();
                var limited = new byte[32 * 20]; inbound.GetStream().ReadExactly(limited);
                Check(!inbound.GetStream().DataAvailable, "Ten-finger cap and unknown move/up suppression");
                Check(Enumerable.Range(0, 10).All(i => limited[i * 32 + 1] == 0 && limited[(i + 10) * 32 + 1] == 1), "Every accepted finger is released after reaching the cap");
                var aim = new AimGesture(transport); aim.Begin(p);
                transport.Touch(10, 0, .88, .76); transport.Touch(11, 0, .968, .53);
                mouse.Add(0, 20, -10, 1920, 1080); var movement = mouse.Drain();
                aim.Move(p, movement.X, movement.Y, 1280, 590);
                var aimingPackets = new byte[32 * 4]; inbound.GetStream().ReadExactly(aimingPackets);
                Check(aimingPackets[1] == 0 && BinaryPrimitives.ReadInt64BigEndian(aimingPackets.AsSpan(2)) == 1 &&
                    aimingPackets[97] == 2 && BinaryPrimitives.ReadInt64BigEndian(aimingPackets.AsSpan(98)) == 1 &&
                    BinaryPrimitives.ReadInt32BigEndian(aimingPackets.AsSpan(106)) > BinaryPrimitives.ReadInt32BigEndian(aimingPackets.AsSpan(10)),
                    "Actual camera controller moves while fire and scope fingers remain held");
                aim.Move(p, 1000, 0, 1280, 590);
                transport.Touch(10, 1, .88, .76); transport.Touch(11, 1, .968, .53); transport.ReleaseAll(); aim.Reset();
                var renewal = new List<byte[]>();
                while (true) {
                    var packet = new byte[32]; inbound.GetStream().ReadExactly(packet); renewal.Add(packet);
                    if (packet[1] == 1 && BinaryPrimitives.ReadInt64BigEndian(packet.AsSpan(2)) == 10) break;
                }
                Check(renewal.Take(renewal.Count - 1).All(packet => BinaryPrimitives.ReadInt64BigEndian(packet.AsSpan(2)) == 1) &&
                    renewal.Any(packet => packet[1] == 1) && renewal.Count(packet => packet[1] == 2) == 19,
                    "Large mouse sweeps renew only the camera finger without dropping movement or releasing held actions");
                var releases = new byte[64]; inbound.GetStream().ReadExactly(releases);
                Check(releases[1] == 1 && BinaryPrimitives.ReadInt64BigEndian(releases.AsSpan(2)) == 11 &&
                    releases[33] == 1 && BinaryPrimitives.ReadInt64BigEndian(releases.AsSpan(34)) == 1 && !inbound.GetStream().DataAvailable,
                    "Scope release and aim cleanup release their own fingers exactly once");
                aim.Begin(p); transport.ReleaseAll(); aim.Reset();
                var restarted = new byte[64]; inbound.GetStream().ReadExactly(restarted);
                Check(restarted[1] == 0 && restarted[33] == 1, "Camera can begin a fresh aim session after cleanup");
            } finally { listener.Stop(); }
            // Run desktop-capture fixtures after the deterministic controller
            // and transport checks, which do not depend on desktop visibility.
            DrivingDetectionTest.Run(Fixture, Check);
            results.Add($"{results.Count} checks passed.");
            File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "self-test-results.txt"), results); return 0;
        } catch (Exception e) {
            results.Add("FAIL " + e); File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "self-test-results.txt"), results); return 1;
        }
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference<MappingOverlay> CreateOverlay() => new(new MappingOverlay());
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool PaintAndDispose(WeakReference<MappingOverlay> weak)
    {
        if (!weak.TryGetTarget(out var overlay)) return false;
        overlay.Update(new(-3000, -3000, 80, 80), Profile.Starter(), true, false, -1, true);
        overlay.Dispose(); overlay.Dispose();
        return true;
    }
}
