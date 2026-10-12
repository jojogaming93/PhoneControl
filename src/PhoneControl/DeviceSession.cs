// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace PhoneControl;

public sealed class ToolPaths
{
    public string Scrcpy { get; }
    public string Adb { get; }
    public string Server => Path.Combine(Path.GetDirectoryName(Scrcpy)!, "scrcpy-server");
    public string ControlServer => Path.Combine(AppContext.BaseDirectory, "tools", "phonecontrol-control.jar");
    public ToolPaths()
    {
        Scrcpy = Find("scrcpy.exe") ?? throw new FileNotFoundException("PhoneControl's video engine is missing. Run the PhoneControl installer again to repair it.");
        var bundledAdb = Path.Combine(Path.GetDirectoryName(Scrcpy)!, "adb.exe");
        Adb = File.Exists(bundledAdb) ? bundledAdb : Find("adb.exe") ?? throw new FileNotFoundException("ADB was not found.");
        if (!File.Exists(Server)) throw new FileNotFoundException("scrcpy-server is missing beside scrcpy.exe.");
        if (!File.Exists(ControlServer)) throw new FileNotFoundException("PhoneControl's control server is missing. Republish the complete app folder.");
    }
    private static string? Find(string exe)
    {
        foreach (var root in new[] { Path.Combine(AppContext.BaseDirectory, "tools", "scrcpy"), Path.Combine(AppContext.BaseDirectory, "..", "..", "tools", "scrcpy") }
                     .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')))
        {
            var p = Path.Combine(root.Trim('"'), exe);
            if (File.Exists(p)) return Path.GetFullPath(p);
        }
        string winget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(winget))
            foreach (var dir in Directory.EnumerateDirectories(winget, "Genymobile.scrcpy_*"))
                foreach (var sub in Directory.EnumerateDirectories(dir, "scrcpy-win64-*"))
                    if (File.Exists(Path.Combine(sub, exe))) return Path.Combine(sub, exe);
        return null;
    }
}

public static class Processes
{
    public static Process Start(string exe, IEnumerable<string> args, Action<string>? output = null)
    {
        var info = new ProcessStartInfo(exe) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output?.Invoke(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output?.Invoke(e.Data); };
        process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
        return process;
    }
    public static async Task<string> Run(string exe, params string[] args)
    {
        var lines = new List<string>();
        using var p = Start(exe, args, line => { lock (lines) lines.Add(line); });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try { await p.WaitForExitAsync(timeout.Token); }
        catch { try { p.Kill(true); } catch { } throw new TimeoutException($"{Path.GetFileName(exe)} did not respond in 15 seconds."); }
        p.WaitForExit();
        string result; lock (lines) result = string.Join('\n', lines);
        if (p.ExitCode != 0) throw new IOException(result);
        return result;
    }
    public static void Stop(Process? p)
    {
        if (p is null) return;
        try { if (!p.HasExited) p.Kill(true); } catch { }
        p.Dispose();
    }
}

// Android adapter: ADB lifecycle, scrcpy framing and the control socket stay here.
public sealed class DeviceSession : IPhoneSession
{
    private readonly ToolPaths tools;
    private readonly object gate = new();
    private readonly Dictionary<long, (double X, double Y)> active = new();
    private TcpClient? client;
    private NetworkStream? stream;
    private Process? server, mirror;
    private string serial = "";
    private int port;
    private string remoteJar = "";
    private int naturalWidth, naturalHeight;
    private int width, height;
    private int textureWidth, textureHeight;
    public nint MirrorWindow { get; private set; }
    public int Width => Volatile.Read(ref width);
    public int Height => Volatile.Read(ref height);
    public int TextureWidth => Volatile.Read(ref textureWidth);
    public int TextureHeight => Volatile.Read(ref textureHeight);
    public bool Connected => stream is not null;
    public bool MirrorAlive => mirror is { HasExited: false };
    public event Action<string>? Statistics;
    public event Action? GeometryChanged;
    public event Action<string>? Fault;
    public DeviceSession(ToolPaths tools) => this.tools = tools;
    public async Task<byte[]> CaptureFrame(CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var info = new ProcessStartInfo(tools.Adb) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { "-s", serial, "exec-out", "screencap", "-p" }) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new IOException("Could not capture phone HUD.");
        try {
            var errors = process.StandardError.ReadToEndAsync(timeout.Token);
            using var output = new MemoryStream();
            var buffer = new byte[65536];
            int count;
            while ((count = await process.StandardOutput.BaseStream.ReadAsync(buffer, timeout.Token)) != 0) {
                if (output.Length + count > 16_000_000) throw new IOException("Phone HUD capture exceeded the size limit.");
                output.Write(buffer, 0, count);
            }
            await process.WaitForExitAsync(timeout.Token);
            string error = await errors;
            if (process.ExitCode != 0) throw new IOException("Phone HUD capture failed: " + error.Trim());
            return output.ToArray();
        } finally { if (!process.HasExited) { try { process.Kill(true); } catch (InvalidOperationException) { } } }
    }
    internal static DeviceSession ForTransportTest(NetworkStream stream, int width, int height) =>
        new(null!) { stream = stream, width = width, height = height };

    internal static (int Width, int Height) ParseDisplaySize(string output)
    {
        // Android's last size is the effective override when one is present.
        var matches = Regex.Matches(output, @"(\d+)x(\d+)");
        if (matches.Count == 0 || !int.TryParse(matches[^1].Groups[1].Value, out int w) ||
            !int.TryParse(matches[^1].Groups[2].Value, out int h) || w < 1 || h < 1 ||
            w > ushort.MaxValue || h > ushort.MaxValue)
            throw new IOException("The phone did not report a valid display size. Reconnect it and check diagnostics.");
        return (w, h);
    }

    public async Task Connect(PhoneDevice device, PhoneConnectionOptions options)
    {
        if (Connected) throw new InvalidOperationException("Already connected.");
        if (device.State != PhoneDeviceState.Ready) throw new IOException("Unlock the phone and accept its USB debugging prompt.");
        serial = device.Id;
        var versionOutput = await Processes.Run(tools.Scrcpy, "--version");
        var version = Regex.Match(versionOutput, @"scrcpy\s+(\d+\.\d+(?:\.\d+)?)").Groups[1].Value;
        if (version != "4.1") throw new IOException($"This release uses the scrcpy 4.1 protocol. Found {version}. Install 4.1 with Install-Dependencies.ps1.");
        try {
            string size = await Processes.Run(tools.Adb, "-s", serial, "shell", "wm", "size");
            (naturalWidth, naturalHeight) = ParseDisplaySize(size);
            width = textureWidth = naturalWidth;
            height = textureHeight = naturalHeight;
            string id = Random.Shared.Next(1, int.MaxValue).ToString("x8");
            remoteJar = $"/data/local/tmp/phonecontrol-{id}.jar";
            await Processes.Run(tools.Adb, "-s", serial, "push", tools.ControlServer, remoteJar);
            string forwarded = await Processes.Run(tools.Adb, "-s", serial, "forward", "tcp:0", $"localabstract:scrcpy_{id}");
            port = int.Parse(forwarded.Trim());
            server = Processes.Start(tools.Adb, ["-s", serial, "shell", $"CLASSPATH={remoteJar}", "app_process", "/",
                "com.genymobile.scrcpy.Server", version, $"scid={id}", "video=false", "audio=false", "control=true", "tunnel_forward=true",
                "send_device_meta=false", "send_dummy_byte=true", "clipboard_autosync=false", "power_on=false", "cleanup=false"], Log.Write);
            // Forwarding may accept TCP before the Android abstract socket exists. Only a dummy-byte handshake proves readiness.
            for (int attempt = 0; attempt < 50; attempt++) {
                var candidate = new TcpClient { NoDelay = true, SendTimeout = 250 };
                try {
                    using var timeout = new CancellationTokenSource(500);
                    await candidate.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
                    var candidateStream = candidate.GetStream();
                    byte[] dummy = new byte[1];
                    int got = await candidateStream.ReadAsync(dummy, timeout.Token);
                    if (got == 1 && dummy[0] == 0) { client = candidate; stream = candidateStream; break; }
                } catch (Exception e) when (e is SocketException or IOException or OperationCanceledException) { }
                candidate.Dispose();
                if (server.HasExited) throw new IOException("Phone control server exited. See diagnostics.");
                await Task.Delay(100);
            }
            if (stream is null) throw new IOException("Could not open the phone control channel.");
            _ = DrainControlAsync(stream);
            var args = new List<string> { "-s", serial, "--no-control", "--video-codec=h264", $"--max-fps={options.MaxFps}",
                $"--max-size={options.MaxSize}", $"--video-bit-rate={options.BitrateMbps}M", "--video-buffer=0", "--print-fps",
                // Keep the renderer's default Alt+Q quit and other Alt shortcuts
                // away from the game's Alt capture key and movement bindings.
                "--shortcut-mod=rsuper", "--window-title=PhoneControl | Phone", "--window-width=1280", "--window-height=720", "--verbosity=info" };
            if (!options.Audio) args.Add("--no-audio");
            else { args.Add("--audio-buffer=30"); }
            mirror = Processes.Start(tools.Scrcpy, args, line => {
                Log.Write(line);
                var texture = Regex.Match(line, @"Texture:\s*(\d+)x(\d+)");
                if (texture.Success) {
                    lock (gate) {
                        ReleaseAll();
                        textureWidth = int.Parse(texture.Groups[1].Value); textureHeight = int.Parse(texture.Groups[2].Value);
                        bool landscape = textureWidth > textureHeight;
                        width = landscape ? Math.Max(naturalWidth, naturalHeight) : Math.Min(naturalWidth, naturalHeight);
                        height = landscape ? Math.Min(naturalWidth, naturalHeight) : Math.Max(naturalWidth, naturalHeight);
                    }
                    GeometryChanged?.Invoke();
                }
                if (line.Contains("fps", StringComparison.OrdinalIgnoreCase)) Statistics?.Invoke(line.Replace("INFO:", "").Trim());
            });
            mirror.Exited += (_, _) => Fault?.Invoke("Phone view closed or USB disconnected.");
            for (int i = 0; i < 100; i++) {
                if (mirror.HasExited) throw new IOException("Phone mirror could not start. See diagnostics.");
                mirror.Refresh();
                if (mirror.MainWindowHandle != 0) { MirrorWindow = mirror.MainWindowHandle; break; }
                await Task.Delay(100);
            }
            if (MirrorWindow == 0) throw new IOException("Phone view did not create a window.");
            Log.Write($"Connected. {Width}×{Height}, H.264, {options.MaxFps} FPS cap, {options.MaxSize}px, no added video buffering.");
        } catch { await Disconnect(); throw; }
    }
    private async Task DrainControlAsync(NetworkStream current)
    {
        try { var b = new byte[4096]; while (await current.ReadAsync(b) > 0) { } }
        catch (Exception e) when (e is IOException or ObjectDisposedException) { }
        if (ReferenceEquals(stream, current)) Fault?.Invoke("USB control channel disconnected.");
    }
    public static byte[] Packet(long id, byte action, int x, int y, int w, int h)
    {
        var b = new byte[32]; b[0] = 2; b[1] = action;
        BinaryPrimitives.WriteInt64BigEndian(b.AsSpan(2), id);
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(10), Math.Clamp(x, 0, w - 1));
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(14), Math.Clamp(y, 0, h - 1));
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(18), checked((ushort)w));
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(20), checked((ushort)h));
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(22), action == 1 ? (ushort)0 : ushort.MaxValue);
        return b;
    }
    public void Touch(long pointer, byte action, double x, double y)
    {
        lock (gate) {
            if (stream is null) return;
            if (action == 0 && !active.ContainsKey(pointer) && active.Count >= 10) return;
            if (action != 0 && !active.ContainsKey(pointer)) return;
            try {
                if (Log.TraceTouches && action != 2) Log.Write($"Touch #{pointer}: {(action == 0 ? "DOWN" : "UP")} at {x:F3},{y:F3} (display {width}×{height})");
                stream.Write(Packet(pointer, action, (int)Math.Round(x * (width - 1)), (int)Math.Round(y * (height - 1)), width, height));
                if (action == 1) active.Remove(pointer); else active[pointer] = (x, y);
            } catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException) {
                active.Clear(); stream = null;
                Fault?.Invoke("Control transport failed: " + e.Message);
            }
        }
    }
    public void Back() => AndroidKey(4);

    private void AndroidKey(int code)
    {
        lock (gate) {
            if (stream is null) return;
            try {
                var b = new byte[14]; BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(2), code);
                stream.Write(b); b[1] = 1; stream.Write(b);
            } catch (Exception e) { Fault?.Invoke(e.Message); }
        }
    }
    public void ReleaseAll()
    {
        lock (gate) {
            foreach (var (id, pos) in active.ToArray()) Touch(id, 1, pos.X, pos.Y);
            active.Clear();
        }
    }
    public async Task Disconnect()
    {
        lock (gate) { ReleaseAll(); stream = null; client?.Dispose(); client = null; }
        Processes.Stop(mirror); mirror = null; MirrorWindow = 0;
        Processes.Stop(server); server = null;
        if (port != 0) { try { await Processes.Run(tools.Adb, "-s", serial, "forward", "--remove", $"tcp:{port}"); } catch { } port = 0; }
        if (remoteJar.Length > 0) { try { await Processes.Run(tools.Adb, "-s", serial, "shell", "rm", "-f", remoteJar); } catch { } remoteJar = ""; }
    }
    public void Dispose()
    {
        lock (gate) { ReleaseAll(); stream = null; client?.Dispose(); }
        Processes.Stop(mirror); Processes.Stop(server);
    }
}
