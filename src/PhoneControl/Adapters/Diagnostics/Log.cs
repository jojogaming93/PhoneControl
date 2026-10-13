// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only


using PhoneControl.Adapters.Persistence;

namespace PhoneControl.Adapters.Diagnostics;

public static class Log
{
    public static bool TraceTouches { get; set; }
    private static readonly object Gate = new();
    public static string PathName => Path.Combine(JsonSettingsStore.DirectoryPath, "phonecontrol.log");
    public static event Action<string>? Message;
    public static void Write(string text)
    {
        lock (Gate) {
            try {
                Directory.CreateDirectory(JsonSettingsStore.DirectoryPath);
                if (File.Exists(PathName) && new FileInfo(PathName).Length > 4_000_000)
                    File.Move(PathName, PathName + ".previous", true);
                File.AppendAllText(PathName, $"{DateTimeOffset.Now:O} {text}{Environment.NewLine}");
            } catch { }
        }
        Message?.Invoke(text);
    }
}
