// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;



namespace PhoneControl.Adapters.Processes;

public static class ProcessRunner
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
