// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PhoneControl;

internal static class IosBackendTest
{
    internal static async Task Run(Action<bool, string> check) {
        foreach (string invalid in new[] { "file:///tmp/test", "not a URL", "http://user:secret@localhost:8100/", "http://localhost:8100/?token=test", "http://localhost:8100/#test" }) {
            bool rejected = false; try { IosWdaClient.ValidateEndpoint(invalid); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "iPhone endpoint rejects unsupported schemes, embedded credentials and URL parameters");
        }
        var oldSettings = JsonSerializer.Deserialize<Settings>("{\"AutoConnect\":false}")!;
        check(oldSettings.PhoneBackend == "android" && oldSettings.IPhoneEndpoint == "http://127.0.0.1:8100/" && !oldSettings.AutoConnect,
            "Existing settings default to Android without resetting preferences");
        var edited = new Settings { PhoneBackend = "iphone", IPhoneEndpoint = "http://127.0.0.1:8101/" };
        edited.Profile.Sensitivity = 2.7;
        var roundtrip = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(edited))!;
        check(roundtrip.PhoneBackend == "iphone" && roundtrip.IPhoneEndpoint == edited.IPhoneEndpoint && roundtrip.Profile.Sensitivity == 2.7,
            "iPhone connection settings persist alongside unchanged edited profiles");
        check(IosMirrorWindow.MapPoint(50, 50, 100, 100, 64, 128) == (.5, .5) &&
            IosMirrorWindow.MapPoint(10, 50, 100, 100, 64, 128) is null &&
            IosMirrorWindow.MapPoint(50, 10, 100, 100, 128, 64) is null,
            "Portrait and landscape viewer coordinates exclude letterboxing");
        using var server = new MockWda();
        var backend = new IosPhoneBackend(server.Endpoint);
        var devices = await backend.DiscoverDevices();
        check(devices.Count == 1 && devices[0].State == PhoneDeviceState.Ready && !ConnectionPolicy.CanAutoConnect(devices) && server.SessionCreates == 0,
            "iPhone discovery reads status without creating a session or auto-connecting a network endpoint");
        server.ActiveSession = "external-session";
        using (var occupied = new IosWdaClient(server.Endpoint)) {
            bool rejected = false; try { await occupied.CreateSession(CancellationToken.None); } catch (IOException) { rejected = true; }
            check(rejected && server.SessionCreates == 0 && server.ActiveSession == "external-session", "An existing WDA session is never taken over");
        }
        server.ActiveSession = null;
        var mirror = new TestMirror();
        using var session = new IosPhoneSession(server.Endpoint, _ => mirror);
        await session.Connect(devices[0], new(1, 1920, 16, true));
        check(server.PngConfigured, "iPhone connection explicitly negotiates PNG instead of relying on WDA JPEG/HEIC defaults");
        check(session.Connected && session.Width == 64 && session.Height == 32 && session.TextureWidth == 128 && session.TextureHeight == 64,
            "iPhone point coordinates and screenshot pixels remain separate");
        check(session.RealtimeInput is null && !session.SupportsRealtimeTouch, "Completed-gesture sessions expose no continuous gameplay input port");
        check(!typeof(ITouchSink).IsInstanceOfType(session) && !typeof(IPhoneInput).IsInstanceOfType(session),
            "iPhone does not implement fake persistent finger or Android Back operations");
        await session.Gesture(new(0, 0, 1, 1, 400));
        using (var json = JsonDocument.Parse(server.Actions.Last())) {
            var actions = json.RootElement.GetProperty("actions")[0].GetProperty("actions");
            check(actions[0].GetProperty("x").GetInt32() == 0 && actions[2].GetProperty("x").GetInt32() == 63 &&
                actions[2].GetProperty("y").GetInt32() == 31 && actions[2].GetProperty("duration").GetInt32() == 400 &&
                actions[1].GetProperty("type").GetString() == "pointerDown" && actions[3].GetProperty("type").GetString() == "pointerUp",
                "A complete iPhone swipe uses viewport points and ends with pointerUp");
        }
        await session.Gesture(new(.5, .5, .5, .5, 1000));
        using (var json = JsonDocument.Parse(server.Actions.Last()))
            check(json.RootElement.GetProperty("actions")[0].GetProperty("actions")[2].GetProperty("type").GetString() == "pause",
                "An iPhone long press remains a finite gesture with an explicit lift");
        await session.Home();
        check(server.HomePresses == 1, "Home uses the owned session's iOS button route");
        server.SlowAction = true;
        var pendingGesture = session.Gesture(new(0, 0, 1, 1, 100));
        bool busy = false; try { await session.Gesture(new(0, 0, 1, 1, 100)); } catch (IOException) { busy = true; }
        await pendingGesture; server.SlowAction = false;
        check(busy && server.Actions.Count == 3, "Overlapping iPhone gestures are rejected instead of accumulating in a queue");
        int beforeInvalid = server.Actions.Count;
        foreach (var gesture in new[] { new PhoneGesture(double.NaN, 0, .5, .5, 100), new PhoneGesture(0, 0, 1, 1, 2001) }) {
            bool rejected = false; try { await session.Gesture(gesture); } catch (InvalidDataException) { rejected = true; }
            check(rejected && server.Actions.Count == beforeInvalid, "Invalid or unbounded iPhone gestures never reach the server");
        }
        int changes = 0; session.GeometryChanged += () => Interlocked.Increment(ref changes);
        server.Portrait = true;
        var portraitPng = await session.CaptureFrame(CancellationToken.None);
        check(session.Width == 32 && session.Height == 64 && session.TextureWidth == 64 && session.TextureHeight == 128 && changes > 0,
            "iPhone orientation updates viewport and texture dimensions and notifies subscribers");
        var decoded = await DrivingModeMonitor.DecodeFrame(portraitPng);
        check(decoded.Width == 64 && decoded.Height == 128 && decoded.Pixels.Length == 64 * 128 * 4,
            "Synthetic iPhone PNGs decode through the real Windows image codec");
        server.Portrait = false;
        bool stale = false; try { await session.Gesture(new(0, 0, 1, 1, 100)); } catch (IOException) { stale = true; }
        check(stale && server.Actions.Count == beforeInvalid, "A stale viewport cannot inject a gesture after rotation");
        await session.Disconnect(); await session.Disconnect();
        check(!session.Connected && !mirror.Alive && server.SessionDeletes == 1 && server.ActiveSession is null,
            "iPhone disconnect drains capture and deletes only its owned session, once");
        server.SlowScreenshot = true;
        server.ScreenshotEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var cancelledSession = new IosPhoneSession(server.Endpoint, _ => new TestMirror()))
        using (var cancellation = new CancellationTokenSource()) {
            var connecting = cancelledSession.Connect(devices[0], new(1, 1920, 16, false), cancellation.Token);
            await server.ScreenshotEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cancellation.Cancel();
            bool cancelled = false;
            try { await connecting; }
            catch (OperationCanceledException) { cancelled = true; }
            check(cancelled && !cancelledSession.Connected && server.SessionDeletes == 2 && server.ActiveSession is null,
                "Cancelling iPhone connection cleans up the created WDA session before opening a viewer");
        }
        server.ScreenshotEntered = null;
        server.SlowScreenshot = false;
        using var client = new IosWdaClient(server.Endpoint);
        server.ActiveSession = "test-session";
        server.DecimalSize = true;
        check(await client.Size("test-session", CancellationToken.None) == (64, 32), "WDA viewport points accept CGRect numbers serialized with decimal notation");
        server.DecimalSize = false;
        server.SlowScreenshot = true;
        using (var cancellation = new CancellationTokenSource(40)) {
            bool cancelled = false; try { await client.Frame("test-session", cancellation.Token); } catch (OperationCanceledException) { cancelled = true; }
            check(cancelled, "Cancelling an iPhone screenshot cancels the HTTP operation");
        }
        server.SlowScreenshot = false; server.OversizedStatus = true;
        bool oversized = false; try { await client.Status(CancellationToken.None); } catch (IOException) { oversized = true; }
        check(oversized, "Oversized WDA responses are rejected before buffering their payload");
        server.OversizedStatus = false; server.ProtocolError = true;
        bool error = false; try { await client.Status(CancellationToken.None); } catch (IOException) { error = true; }
        check(error, "WDA protocol errors are rejected even with HTTP 200");
        server.ProtocolError = false; server.NotIPhone = true;
        bool notIPhone = false; try { await backend.DiscoverDevices(); } catch (IOException) { notIPhone = true; }
        check(notIPhone, "Other WebDriver servers cannot masquerade as an iPhone endpoint");
        bool badPng = false; try { IosWdaClient.PngSize(new byte[33]); } catch (IOException) { badPng = true; }
        check(badPng, "Invalid screenshots cannot publish display geometry");
    }

    private sealed class TestMirror : IIosMirror {
        public nint Window => 0;
        public bool Alive { get; private set; } = true;
        public void Present(IosFrame frame) { }
        public void Dispose() => Alive = false;
    }
    private sealed class MockWda : IDisposable {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly Task loop;
        private readonly ConcurrentBag<Task> requests = [];
        private readonly byte[] landscape = Png(128, 64), portrait = Png(64, 128);
        internal string Endpoint { get; }
        internal string? ActiveSession;
        internal volatile bool Portrait, SlowScreenshot, SlowAction, OversizedStatus, ProtocolError, NotIPhone, DecimalSize;
        internal int SessionCreates, SessionDeletes, HomePresses;
        internal bool PngConfigured;
        internal TaskCompletionSource? ScreenshotEntered;
        internal ConcurrentQueue<string> Actions { get; } = new();
        internal MockWda() {
            listener.Start(); Endpoint = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";
            loop = Task.Run(async () => {
                try {
                    while (!stop.IsCancellationRequested) {
                        var socket = await listener.AcceptTcpClientAsync(stop.Token);
                        requests.Add(Handle(socket));
                    }
                } catch (OperationCanceledException) { }
            });
        }
        private async Task Handle(TcpClient socket) {
            using (socket) try {
                var stream = socket.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
                string[] line = (await reader.ReadLineAsync(stop.Token) ?? "").Split(' ');
                int length = 0; string? header;
                while (!string.IsNullOrEmpty(header = await reader.ReadLineAsync(stop.Token)))
                    if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = int.Parse(header.Split(':')[1]);
                if (line.Length < 2 || length > 100000) return;
                var chars = new char[length]; if (length > 0) await reader.ReadBlockAsync(chars, stop.Token);
                string body = new(chars), method = line[0], path = line[1];
                JsonNode? value = null;
                if (path == "/status") {
                    if (OversizedStatus) {
                        await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {IosWdaClient.MaxResponseBytes + 1}\r\nConnection: close\r\n\r\n"), stop.Token); return;
                    }
                    value = ProtocolError ? new JsonObject { ["error"] = "invalid argument" } :
                        new JsonObject { ["ready"] = true, ["os"] = new JsonObject { ["name"] = "iOS" }, ["device"] = NotIPhone ? "ipad" : "iphone" };
                } else if (path == "/session" && method == "POST") {
                    using var json = JsonDocument.Parse(body);
                    if (json.RootElement.GetProperty("capabilities").GetProperty("alwaysMatch").GetProperty("appium:shouldTerminateApp").GetBoolean())
                        throw new InvalidOperationException("Client attempted to terminate the foreground app.");
                    Interlocked.Increment(ref SessionCreates); ActiveSession = "test-session";
                    PngConfigured = false;
                    value = new JsonObject { ["sessionId"] = ActiveSession, ["capabilities"] = new JsonObject { ["device"] = "iphone" } };
                } else if (path == "/session/test-session/appium/settings" && method == "POST") {
                    using var json = JsonDocument.Parse(body);
                    PngConfigured = json.RootElement.GetProperty("settings").GetProperty("screenshotQuality").GetInt32() == 0;
                    value = new JsonObject { ["screenshotQuality"] = 0 };
                } else if (path.EndsWith("/window/size")) value = new JsonObject { ["width"] = Portrait ? 32 : 64, ["height"] = Portrait ? 64 : 32 };
                else if (path.EndsWith("/screenshot")) {
                    if (SlowScreenshot) { ScreenshotEntered?.TrySetResult(); await Task.Delay(300, stop.Token); }
                    value = JsonValue.Create(Convert.ToBase64String(PngConfigured ? (Portrait ? portrait : landscape) : new byte[] { 255, 216, 255, 217 }));
                } else if (path == "/session/test-session" && method == "DELETE") {
                    Interlocked.Increment(ref SessionDeletes); ActiveSession = null;
                } else if (path == "/session/test-session/actions" && method == "POST") {
                    if (SlowAction) await Task.Delay(150, stop.Token);
                    Actions.Enqueue(body);
                }
                else if (path == "/session/test-session/wda/pressButton" && method == "POST") {
                    using var json = JsonDocument.Parse(body);
                    if (json.RootElement.GetProperty("name").GetString() == "home") Interlocked.Increment(ref HomePresses);
                } else throw new InvalidOperationException("Unexpected WDA test route: " + path);
                string response = new JsonObject { ["sessionId"] = ActiveSession, ["value"] = value }.ToJsonString();
                if (DecimalSize && path.EndsWith("/window/size")) response = response.Replace(":64", ":64.0").Replace(":32", ":32.0");
                var bytes = Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"), stop.Token);
                await stream.WriteAsync(bytes, stop.Token);
            } catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            catch (IOException) { /* A cancelled HTTP client can close its socket. */ }
        }
        public void Dispose() {
            stop.Cancel(); listener.Stop(); loop.GetAwaiter().GetResult();
            Task.WhenAll(requests).GetAwaiter().GetResult(); stop.Dispose();
        }
        private static byte[] Png(int width, int height) {
            using var png = new MemoryStream(); png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
            void Chunk(string type, byte[] bytes) {
                var length = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length); png.Write(length);
                byte[] all = Encoding.ASCII.GetBytes(type).Concat(bytes).ToArray(); png.Write(all);
                uint crc = 0xffffffff;
                foreach (byte b in all) { crc ^= b; for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0); }
                BinaryPrimitives.WriteUInt32BigEndian(length, ~crc); png.Write(length);
            }
            var header = new byte[13]; BinaryPrimitives.WriteInt32BigEndian(header, width); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height); header[8] = 8;
            Chunk("IHDR", header);
            using var compressed = new MemoryStream();
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true)) {
                var row = Enumerable.Repeat((byte)120, width + 1).ToArray(); row[0] = 0;
                for (int y = 0; y < height; y++) zlib.Write(row);
            }
            Chunk("IDAT", compressed.ToArray()); Chunk("IEND", []); return png.ToArray();
        }
    }
}
