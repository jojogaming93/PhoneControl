// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;



namespace PhoneControl.Adapters.Android;

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
