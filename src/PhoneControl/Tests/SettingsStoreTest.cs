// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Text.Json;
using PhoneControl.Adapters.Persistence;
using PhoneControl.Application.Ports;
using PhoneControl.Domain.Profiles;

namespace PhoneControl.Tests;

internal static class SettingsStoreTest
{
    public static void Run(Action<bool, string> check)
    {
        string directory = Path.Combine(Path.GetTempPath(), "PhoneControl-store-" + Guid.NewGuid().ToString("N"));
        try {
            ISettingsStore store = new JsonSettingsStore(directory);
            var settings = store.Load();
            settings.AutoConnect = false; settings.Profile.Name = "Edited port layout";
            settings.Profile.Sensitivity = 2.7; settings.Profile.Bindings[0].X = .63;
            store.Save(settings);
            var loaded = store.Load();
            check(!loaded.AutoConnect && loaded.Profile.Name == settings.Profile.Name &&
                loaded.Profile.Sensitivity == 2.7 && loaded.Profile.Bindings[0].X == .63 && loaded.Profile.Driving is not null,
                "The settings port preserves edited profiles and preferences across save/load");
            string path = Path.Combine(directory, "settings.json");
            var legacy = new Settings();
            legacy.Profile.Bindings.RemoveAll(binding => binding.Key == "=");
            legacy.Profile.Bindings.Add(new() { Name = "Sprint", Key = "Shift", Mode = "Hold", X = .41, Y = .32 });
            File.WriteAllText(path, JsonSerializer.Serialize(legacy));
            var migrated = store.Load();
            var autorun = migrated.Profile.Bindings.Last();
            check(autorun.Name == "Autorun" && autorun.Key == "=" && autorun.Mode == "Tap" && autorun.X == .41 && autorun.Y == .32 &&
                !File.ReadAllText(path).Contains("\"Sprint\""),
                "The persistence adapter retains positions and persists legacy sprint migration");
            File.WriteAllText(path, "invalid-json");
            check(store.Load().Profile.Name == Profile.Starter().Name,
                "Invalid persisted settings recover to starter data through the settings port");
        } finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}
