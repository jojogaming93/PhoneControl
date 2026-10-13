// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using PhoneControl.Domain.Phones;

namespace PhoneControl.Adapters.Ios;

// Direct WebDriverAgent REST client, not an Appium server client. No Apple
// tooling, device IDs, packages or credentials are embedded in PhoneControl.
internal sealed class IosWdaClient : IDisposable
{
    internal const int MaxResponseBytes = 22_000_000;
    private readonly HttpClient http;
    internal Uri Endpoint { get; }
    internal IosWdaClient(string endpoint) {
        Endpoint = ValidateEndpoint(endpoint);
        http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) {
            BaseAddress = Endpoint, Timeout = Timeout.InfiniteTimeSpan
        };
    }
    internal static Uri ValidateEndpoint(string endpoint) {
        if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(uri.Host) ||
            uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new InvalidDataException("Enter a WebDriverAgent HTTP(S) address without credentials, query or fragment.");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }
    internal async Task<JsonElement> Request(HttpMethod method, string path, JsonNode? body, CancellationToken cancellation) {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.Content.Headers.ContentLength is long length && length > MaxResponseBytes)
                throw new IOException("WebDriverAgent response exceeded the size limit.");
            using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var output = new MemoryStream();
            var buffer = new byte[65536]; int count;
            while ((count = await source.ReadAsync(buffer, timeout.Token)) != 0) {
                if (output.Length + count > MaxResponseBytes) throw new IOException("WebDriverAgent response exceeded the size limit.");
                output.Write(buffer, 0, count);
            }
            using var json = JsonDocument.Parse(output.ToArray());
            var root = json.RootElement;
            if (!response.IsSuccessStatusCode || root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("value", out var value))
                throw new IOException($"WebDriverAgent request failed (HTTP {(int)response.StatusCode}).");
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("error", out var error))
                throw new IOException("WebDriverAgent reported: " + (error.ValueKind == JsonValueKind.String ? error.GetString() : "invalid error response"));
            return root.Clone();
        } catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) {
            throw new TimeoutException("WebDriverAgent did not respond within five seconds.");
        } catch (JsonException e) { throw new IOException("WebDriverAgent returned invalid JSON.", e); }
    }
    internal async Task<WdaStatus> Status(CancellationToken cancellation) {
        var root = await Request(HttpMethod.Get, "status", null, cancellation);
        var value = root.GetProperty("value");
        if (value.ValueKind != JsonValueKind.Object) throw new IOException("WebDriverAgent returned invalid status.");
        if (!root.TryGetProperty("sessionId", out var ownership) || ownership.ValueKind is not (JsonValueKind.Null or JsonValueKind.String))
            throw new IOException("WebDriverAgent did not report session ownership.");
        if (!value.TryGetProperty("os", out var os) || os.ValueKind != JsonValueKind.Object || !os.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String || name.GetString() != "iOS" ||
            !value.TryGetProperty("device", out var device) || device.ValueKind != JsonValueKind.String || device.GetString() != "iphone" ||
            !value.TryGetProperty("ready", out var ready) || ready.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new IOException("The endpoint is not an iPhone WebDriverAgent server.");
        return new(ready.GetBoolean(), SessionId(root));
    }
    internal static string? SessionId(JsonElement root) {
        if (root.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty("sessionId", out var nested) && nested.ValueKind == JsonValueKind.String)
            return nested.GetString();
        return root.TryGetProperty("sessionId", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
    }
    internal async Task<string> CreateSession(CancellationToken cancellation) {
        var status = await Status(cancellation);
        if (!status.Ready) throw new IOException("WebDriverAgent is not ready.");
        // WDA's POST /session destroys the previous session. Never take it over.
        if (!string.IsNullOrEmpty(status.ActiveSession))
            throw new IOException("WebDriverAgent already has a session. Use a dedicated instance or close that session first.");
        var capabilities = new JsonObject {
            ["platformName"] = "iOS", ["appium:shouldTerminateApp"] = false,
            ["appium:shouldWaitForQuiescence"] = false, ["appium:waitForIdleTimeout"] = 0
        };
        var root = await Request(HttpMethod.Post, "session", new JsonObject {
            ["capabilities"] = new JsonObject { ["alwaysMatch"] = capabilities, ["firstMatch"] = new JsonArray(new JsonObject()) }
        }, cancellation);
        return SessionId(root) is string id && !string.IsNullOrWhiteSpace(id) ? id :
            throw new IOException("WebDriverAgent did not return a session ID.");
    }
    internal static string SessionPath(string id) => "session/" + Uri.EscapeDataString(id);
    internal async Task<(int Width, int Height)> Size(string id, CancellationToken cancellation) {
        var value = (await Request(HttpMethod.Get, SessionPath(id) + "/window/size", null, cancellation)).GetProperty("value");
        if (value.ValueKind != JsonValueKind.Object) throw new IOException("WebDriverAgent returned invalid viewport dimensions.");
        if (!value.TryGetProperty("width", out var w) || w.ValueKind != JsonValueKind.Number || !w.TryGetDouble(out double width) ||
            !value.TryGetProperty("height", out var h) || h.ValueKind != JsonValueKind.Number || !h.TryGetDouble(out double height) ||
            !double.IsFinite(width) || !double.IsFinite(height) || width < 1 || height < 1 ||
            width > 8192 || height > 8192 || width * height > 16_000_000)
            throw new IOException("WebDriverAgent returned invalid viewport dimensions.");
        // CGRect/NSNumber may serialize whole points as decimal numbers. Floor
        // fractional extents so integer gesture coordinates stay inside them.
        return ((int)Math.Floor(width), (int)Math.Floor(height));
    }
    internal static (int Width, int Height) PngSize(byte[] png) {
        if (png.Length < 33 || png.Length > 16_000_000 || !png.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(8)) != 13 || !png.AsSpan(12, 4).SequenceEqual("IHDR"u8))
            throw new IOException("WebDriverAgent screenshot is not a bounded PNG.");
        int width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)), height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20));
        if (width < 1 || height < 1 || width > 8192 || height > 8192 || (long)width * height > 16_000_000)
            throw new IOException("WebDriverAgent screenshot dimensions exceed the limit.");
        return (width, height);
    }
    internal async Task<PhoneScreenshot> Frame(string id, CancellationToken cancellation) {
        var before = await Size(id, cancellation);
        var value = (await Request(HttpMethod.Get, SessionPath(id) + "/screenshot", null, cancellation)).GetProperty("value");
        if (value.ValueKind != JsonValueKind.String) throw new IOException("WebDriverAgent did not return a screenshot.");
        byte[] png;
        try { png = Convert.FromBase64String(value.GetString()!); }
        catch (FormatException e) { throw new IOException("WebDriverAgent screenshot has invalid base64.", e); }
        var pixels = PngSize(png);
        var after = await Size(id, cancellation);
        if (before != after || Math.Abs((double)pixels.Width / pixels.Height / ((double)after.Width / after.Height) - 1) > .03)
            throw new IOException("The iPhone orientation changed during capture. Waiting for a stable frame.");
        return new(png, after.Width, after.Height, pixels.Width, pixels.Height);
    }
    internal static JsonObject Actions(PhoneGesture gesture, int width, int height) {
        static bool Unit(double v) => double.IsFinite(v) && v >= 0 && v <= 1;
        if (!Unit(gesture.FromX) || !Unit(gesture.FromY) || !Unit(gesture.ToX) || !Unit(gesture.ToY) ||
            gesture.DurationMilliseconds < 40 || gesture.DurationMilliseconds > 2000 || width < 1 || height < 1)
            throw new InvalidDataException("A gesture needs normalized coordinates and a duration of 40–2000 ms.");
        int x = (int)Math.Round(gesture.FromX * (width - 1)), y = (int)Math.Round(gesture.FromY * (height - 1));
        int tx = (int)Math.Round(gesture.ToX * (width - 1)), ty = (int)Math.Round(gesture.ToY * (height - 1));
        var sequence = new JsonArray {
            new JsonObject { ["type"] = "pointerMove", ["duration"] = 0, ["origin"] = "viewport", ["x"] = x, ["y"] = y },
            new JsonObject { ["type"] = "pointerDown", ["button"] = 0 }
        };
        sequence.Add(x == tx && y == ty ? new JsonObject { ["type"] = "pause", ["duration"] = gesture.DurationMilliseconds } :
            new JsonObject { ["type"] = "pointerMove", ["duration"] = gesture.DurationMilliseconds, ["origin"] = "viewport", ["x"] = tx, ["y"] = ty });
        sequence.Add(new JsonObject { ["type"] = "pointerUp", ["button"] = 0 });
        return new JsonObject { ["actions"] = new JsonArray(new JsonObject {
            ["type"] = "pointer", ["id"] = "phonecontrol-finger", ["parameters"] = new JsonObject { ["pointerType"] = "touch" }, ["actions"] = sequence
        }) };
    }
    public void Dispose() => http.Dispose();
}
