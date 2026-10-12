// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

namespace PhoneControl;

public sealed class IosPhoneBackend(string endpoint) : IPhoneBackend
{
    public async Task<IReadOnlyList<PhoneDevice>> DiscoverDevices() {
        using var client = new IosWdaClient(endpoint);
        var status = await client.Status(CancellationToken.None);
        return [new(client.Endpoint.AbsoluteUri, status.Ready ? PhoneDeviceState.Ready : PhoneDeviceState.Offline,
            "iPhone • WebDriverAgent • completed gestures", PhoneTransport.Network)];
    }
    public IPhoneSession CreateSession() => new IosPhoneSession(endpoint);
}

internal interface IIosMirror : IDisposable
{
    nint Window { get; }
    bool Alive { get; }
    void Present(IosFrame frame);
}

// A session owns only its WDA session ID and viewer. The externally prepared
// WDA runner, phone applications, USB tunnels and any other sessions stay owned
// by their creator. Continuous touch injection is deliberately unsupported.
public sealed class IosPhoneSession : IPhoneSession, IPhoneGestureInput
{
    private readonly IosWdaClient client;
    private readonly Func<IosPhoneSession, IIosMirror> createMirror;
    private readonly CancellationTokenSource stop = new();
    private readonly SemaphoreSlim operations = new(1, 1);
    private string? id;
    private IIosMirror? mirror;
    private Task? loop;
    private volatile bool connected;
    private bool disposed;
    private int busy;
    private IosFrame current = new([], 0, 0, 0, 0);
    private int revision;
    public bool SupportsRealtimeTouch => false;
    public IPhoneInput? RealtimeInput => null;
    public int Width => current.PointWidth;
    public int Height => current.PointHeight;
    public int TextureWidth => current.PixelWidth;
    public int TextureHeight => current.PixelHeight;
    public nint MirrorWindow => mirror?.Window ?? 0;
    public bool Connected => connected;
    public bool MirrorAlive => mirror?.Alive == true;
    internal int GeometryRevision => revision;
    public event Action<string>? Statistics;
    public event Action? GeometryChanged;
    public event Action<string>? Fault;
    public IosPhoneSession(string endpoint) : this(endpoint, session => new IosMirrorWindow(session)) { }
    internal IosPhoneSession(string endpoint, Func<IosPhoneSession, IIosMirror> createMirror) {
        client = new IosWdaClient(endpoint); this.createMirror = createMirror;
    }
    public async Task Connect(PhoneDevice device, PhoneConnectionOptions options, CancellationToken cancellation = default) {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (connected || id is not null || stop.IsCancellationRequested) throw new InvalidOperationException("Create a new iPhone session to reconnect.");
        if (device.State != PhoneDeviceState.Ready || device.Id != client.Endpoint.AbsoluteUri)
            throw new IOException("Choose the configured, ready iPhone endpoint.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, stop.Token);
        try {
            id = await client.CreateSession(linked.Token);
            // WDA can default to JPEG/HEIC. Negotiate lossless PNG explicitly
            // before validating image headers or feeding the Windows decoder.
            await client.Request(HttpMethod.Post, IosWdaClient.SessionPath(id) + "/appium/settings",
                new System.Text.Json.Nodes.JsonObject { ["settings"] = new System.Text.Json.Nodes.JsonObject {
                    ["screenshotQuality"] = 0, ["waitForIdleTimeout"] = 0
                } }, linked.Token);
            current = await client.Frame(id, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            mirror = createMirror(this);
            mirror.Present(current);
            connected = true;
            Statistics?.Invoke("iPhone • PNG screenshots • taps/swipes • no audio or continuous game input");
            loop = Task.Run(() => Poll(Math.Clamp(options.MaxFps, 1, 8), stop.Token));
        } catch { await Disconnect(); throw; }
    }
    private async Task Poll(int fps, CancellationToken cancellation) {
        int failures = 0;
        try {
            while (!cancellation.IsCancellationRequested) {
                try { await CaptureFrame(cancellation); failures = 0; }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception e) {
                    if (++failures == 3) { Fault?.Invoke("iPhone view unavailable: " + e.Message); return; }
                }
                await Task.Delay(1000 / fps, cancellation);
            }
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
    }
    public async Task<byte[]> CaptureFrame(CancellationToken cancellation) {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, stop.Token);
        await operations.WaitAsync(linked.Token).ConfigureAwait(false);
        try {
            if (!connected || id is null) throw new IOException("The iPhone session is disconnected.");
            var frame = await client.Frame(id, linked.Token).ConfigureAwait(false);
            bool changed = frame.PointWidth != Width || frame.PointHeight != Height || frame.PixelWidth != TextureWidth || frame.PixelHeight != TextureHeight;
            Volatile.Write(ref current, frame);
            if (changed) { Interlocked.Increment(ref revision); GeometryChanged?.Invoke(); }
            mirror?.Present(frame);
            return frame.Png;
        } finally { operations.Release(); }
    }
    public Task Gesture(PhoneGesture gesture, CancellationToken cancellation = default) {
        // Validate before queueing any work. Geometry is checked again after
        // waiting behind a capture; a rotated frame must never receive stale input.
        _ = IosWdaClient.Actions(gesture, Width, Height);
        int capturedRevision = revision;
        return Interact(async token => {
            var size = await client.Size(id!, token).ConfigureAwait(false);
            if (capturedRevision != revision || size != (Width, Height))
                throw new IOException("The iPhone orientation changed. Wait for the new frame and retry the gesture.");
            await client.Request(HttpMethod.Post, IosWdaClient.SessionPath(id!) + "/actions",
                IosWdaClient.Actions(gesture, Width, Height), token).ConfigureAwait(false);
        }, cancellation);
    }
    public Task Home(CancellationToken cancellation = default) => Interact(async token => {
        await client.Request(HttpMethod.Post, IosWdaClient.SessionPath(id!) + "/wda/pressButton",
            new System.Text.Json.Nodes.JsonObject { ["name"] = "home" }, token).ConfigureAwait(false);
    }, cancellation);
    private async Task Interact(Func<CancellationToken, Task> action, CancellationToken cancellation) {
        if (!connected) throw new IOException("The iPhone session is disconnected.");
        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) throw new IOException("An iPhone gesture is still running. Wait before sending another.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, stop.Token);
        try {
            await operations.WaitAsync(linked.Token).ConfigureAwait(false);
            try { await action(linked.Token).ConfigureAwait(false); }
            finally { operations.Release(); }
        } finally { Interlocked.Exchange(ref busy, 0); }
    }
    internal void ViewClosed() { if (connected) Fault?.Invoke("iPhone view closed."); }
    public async Task Disconnect() {
        if (disposed) return;
        connected = false; stop.Cancel();
        if (loop is not null) { await loop; loop = null; }
        await operations.WaitAsync();
        try {
            if (id is string owned) {
                id = null;
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await client.Request(HttpMethod.Delete, IosWdaClient.SessionPath(owned), null, timeout.Token); }
                catch (Exception e) { Log.Write("iPhone session cleanup: " + e.Message); }
            }
            mirror?.Dispose(); mirror = null;
        } finally { operations.Release(); }
    }
    public void Dispose() {
        if (disposed) return;
        // Normal lifecycle awaits Disconnect first, including connection failure.
        connected = false; stop.Cancel(); mirror?.Dispose(); mirror = null;
        client.Dispose(); disposed = true;
    }
}
