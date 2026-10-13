// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Text.Json;

using PhoneControl.Adapters.Configuration;
using PhoneControl.Adapters.Diagnostics;
using PhoneControl.Application.Ports;
using PhoneControl.Domain.Profiles;

namespace PhoneControl.Adapters.Persistence;

public sealed class JsonSettingsStore : ISettingsStore
{
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.DataDirectoryName);
    public string Directory { get; }
    private string FilePath => Path.Combine(Directory, "settings.json");
    private readonly bool migrateStable;
    public JsonSettingsStore(string directory, bool migrateStable = false) {
        Directory = directory; this.migrateStable = migrateStable;
    }
    internal static bool ImportSettingsIfMissing(string previousDirectory, string destinationDirectory)
    {
        string source = Path.Combine(previousDirectory, "settings.json");
        string destination = Path.Combine(destinationDirectory, "settings.json");
        if (File.Exists(destination) || !File.Exists(source)) return false;
        System.IO.Directory.CreateDirectory(destinationDirectory);
        File.Copy(source, destination, overwrite: false);
        return true;
    }
    public Settings Load()
    {
        try {
            if (migrateStable && !File.Exists(FilePath)) {
                // The stable installer identity locates settings from a previous
                // product name without keeping obsolete branding in the app.
                using var registration = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{CF1762E2-784A-49DA-AF70-A6753131BE40}_is1");
                if (registration?.GetValue("Inno Setup: App Path") is string installedPath) {
                    string previousName = Path.GetFileName(installedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (!string.IsNullOrWhiteSpace(previousName))
                        ImportSettingsIfMissing(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), previousName), Directory);
                }
            }
            var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath))!;
            bool upgraded = s.Profile.UpgradeSprintMapping(); s.Profile.Validate(); s.Profile.EnsureDrivingLayout();
            if (upgraded) Save(s);
            return s;
        }
        catch (Exception e) { if (File.Exists(FilePath)) Log.Write("Could not load settings: " + e.Message); return new(); }
    }
    public void Save(Settings settings)
    {
        settings.Profile.EnsureDrivingLayout();
        settings.Profile.Validate();
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(FilePath + ".tmp", FilePath, true);
    }
}
