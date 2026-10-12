// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Text.Json;

namespace PhoneControl;

public sealed class Binding
{
    public string Name { get; set; } = "Action";
    public string Key { get; set; } = "E";
    public double X { get; set; } = .8;
    public double Y { get; set; } = .5;
    public string Mode { get; set; } = "Hold";
    public Binding Copy() => new() { Name = Name, Key = Key, X = X, Y = Y, Mode = Mode };
}

public sealed class Profile
{
    public string Name { get; set; } = "Starter layout";
    public double JoystickX { get; set; } = .16;
    public double JoystickY { get; set; } = .77;
    // Radius uses screen HEIGHT, so movement stays circular on any aspect ratio.
    public double JoystickRadius { get; set; } = .105;
    public string SprintKey { get; set; } = "Shift";
    public double SprintRadius { get; set; } = .18;
    public double LookX { get; set; } = .62;
    public double LookY { get; set; } = .48;
    public double Sensitivity { get; set; } = 1.2;
    public string Up { get; set; } = "W";
    public string Down { get; set; } = "S";
    public string Left { get; set; } = "A";
    public string Right { get; set; } = "D";
    public List<Binding> Bindings { get; set; } = [
        new() { Name = "Fire", Key = "MouseLeft", X = .91, Y = .48 },
        new() { Name = "Aim", Key = "MouseRight", X = .84, Y = .59 },
        new() { Name = "Jump", Key = "Space", X = .93, Y = .74 },
        new() { Name = "Crouch", Key = "C", X = .85, Y = .82 },
        new() { Name = "Reload", Key = "R", X = .76, Y = .76, Mode = "Tap" },
        new() { Name = "Interact", Key = "E", X = .74, Y = .56, Mode = "Tap" },
        new() { Name = "Autorun", Key = "=", X = .744, Y = .307, Mode = "Tap" },
        new() { Name = "Weapon 1", Key = "1", X = .64, Y = .9, Mode = "Tap" },
        new() { Name = "Weapon 2", Key = "2", X = .75, Y = .9, Mode = "Tap" },
        new() { Name = "Backpack", Key = "B", X = .94, Y = .94, Mode = "Tap" },
        new() { Name = "Map", Key = "M", X = .9, Y = .12, Mode = "Tap" }
    ];
    public DrivingLayout? Driving { get; set; }

    public void EnsureDrivingLayout() => Driving ??= DrivingLayout.Starter(this);

    public Profile Copy() => JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(this))!;
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("Give the profile a name.");
        static bool Unit(double n) => double.IsFinite(n) && n >= 0 && n <= 1;
        if (!Unit(JoystickX) || !Unit(JoystickY) || !Unit(LookX) || !Unit(LookY))
            throw new InvalidDataException("Control coordinates must be between 0 and 100%.");
        if (!double.IsFinite(JoystickRadius) || JoystickRadius < .01 || JoystickRadius > .3 ||
            !double.IsFinite(SprintRadius) || SprintRadius < .01 || SprintRadius > .45 ||
            !double.IsFinite(Sensitivity) || Sensitivity < .1 || Sensitivity > 10)
            throw new InvalidDataException("Movement radius must be 1–30%; sprint reach must be 1–45%; sensitivity must be 0.1–10.");
        if (Bindings is null || Bindings.Count > 64)
            throw new InvalidDataException("Use at most 64 action bindings. Up to 10 fingers can be active at once.");
        var keys = new HashSet<int>();
        foreach (var k in new[] { Up, Down, Left, Right, SprintKey })
        {
            int vk = KeyNames.Parse(k);
            if (KeyNames.IsWheel(vk) || vk == 0 || vk is 0x1B or 0xA4 or 0x77 or 0x78 or 0x7A)
                throw new InvalidDataException($"'{k}' is unsupported or reserved (Esc, Left Alt, F8, F9, F11).");
            if (!keys.Add(vk)) throw new InvalidDataException($"'{k}' is assigned more than once.");
        }
        foreach (var b in Bindings) {
            KeyNames.ValidateAction(b.Key, keys);
            if (!Unit(b.X) || !Unit(b.Y) || b.Mode is not ("Hold" or "Tap"))
                throw new InvalidDataException($"Invalid coordinates or mode for {b.Name}.");
        }
        Driving?.Validate();
    }

    public static Profile Starter()
    {
        var p = new Profile { Name = "Starter layout", JoystickX = .165, JoystickY = .755, JoystickRadius = .09, LookX = .61, LookY = .48 };
        p.Bindings = [
            new() { Name = "Fire / melee", Key = "MouseLeft", X = .88, Y = .76 },
            new() { Name = "Aim down sights", Key = "MouseRight", X = .968, Y = .53, Mode = "Tap" },
            new() { Name = "Jump", Key = "Space", X = .967, Y = .707 },
            new() { Name = "Crouch", Key = "C", X = .875, Y = .935, Mode = "Tap" },
            new() { Name = "Dodge / roll", Key = "Ctrl", X = .954, Y = .907, Mode = "Tap" },
            new() { Name = "Reload", Key = "R", X = .809, Y = .935, Mode = "Tap" },
            new() { Name = "Interact", Key = "E", X = .647, Y = .826, Mode = "Tap" },
            new() { Name = "Autorun", Key = "=", X = .744, Y = .307, Mode = "Tap" },
            new() { Name = "Swap weapon", Key = "Q", X = .91, Y = .531, Mode = "Tap" },
            new() { Name = "Backpack", Key = "B", X = .023, Y = .7, Mode = "Tap" },
            new() { Name = "Map", Key = "M", X = .937, Y = .129, Mode = "Tap" }
        ];
        return p;
    }

    // Earlier versions mislabeled the HUD's autorun button as Sprint. Preserve
    // the user's HUD position and all other bindings when upgrading that layout.
    public bool UpgradeSprintMapping()
    {
        var legacy = Bindings?.FirstOrDefault(b => b.Name == "Sprint" && b.Key == "Shift" && b.Mode == "Hold");
        if (legacy is null) return false;
        legacy.Name = "Autorun"; legacy.Key = "="; legacy.Mode = "Tap";
        SprintKey = "Shift"; return true;
    }
}

public sealed class DrivingLayout
{
    public List<Binding> Bindings { get; set; } = [];
    public static DrivingLayout Starter(Profile walking)
    {
        var layout = new DrivingLayout();
        void Add(string name, string key, double x, double y, string mode = "Hold") =>
            layout.Bindings.Add(new() { Name = name, Key = key, X = x, Y = y, Mode = mode });
        // Initial driving keys follow the player's saved layout. They can then
        // be edited independently without changing walking or future upgrades.
        Add("Accelerate", walking.Up, .177, .506);
        Add("Reverse", walking.Down, .177, .741);
        Add("Steer left", walking.Left, .745, .679);
        Add("Steer right", walking.Right, .882, .679);
        Add("Boost", walking.SprintKey, .087, .518);
        string Key(string action, string fallback) {
            string preferred = walking.Bindings.FirstOrDefault(b => b.Name.Contains(action, StringComparison.OrdinalIgnoreCase))?.Key ?? fallback;
            if (!layout.Bindings.SelectMany(b => KeyNames.ParseMany(b.Key)).Intersect(KeyNames.ParseMany(preferred)).Any()) return preferred;
            return new[] { fallback }.Concat(Enumerable.Range('A', 26).Select(k => ((char)k).ToString()))
                .First(k => !layout.Bindings.Any(b => KeyNames.ParseMany(b.Key).Contains(KeyNames.Parse(k))));
        }
        Add("Brake", Key("Jump", "Space"), .087, .703);
        Add("Exit vehicle", Key("Interact", "E"), .972, .500, "Tap");
        Add("Horn", Key("Horn", "H"), .972, .644);
        Add("Headlights", Key("Headlights", "L"), .972, .790, "Tap");
        Add("Backpack", Key("Backpack", "B"), .023, .700, "Tap");
        Add("Map", Key("Map", "M"), .937, .129, "Tap");
        return layout;
    }
    public void Validate()
    {
        if (Bindings is null || Bindings.Count > 64) throw new InvalidDataException("Use at most 64 driving bindings.");
        var keys = new HashSet<int>();
        foreach (var binding in Bindings) {
            KeyNames.ValidateAction(binding.Key, keys);
            if (!double.IsFinite(binding.X) || !double.IsFinite(binding.Y) || binding.X is < 0 or > 1 || binding.Y is < 0 or > 1 || binding.Mode is not ("Hold" or "Tap"))
                throw new InvalidDataException($"Invalid driving coordinates or mode for {binding.Name}.");
        }
    }
}

public static class KeyNames
{
    // These are pulse identifiers, outside Windows' virtual-key range.
    public const int WheelUp = 0x100, WheelDown = 0x101;
    public static bool IsWheel(int key) => key is WheelUp or WheelDown;
    public static bool IsMouse(int key) => key is >= 1 and <= 6 || IsWheel(key);
    public static readonly Dictionary<string, int> Special = new(StringComparer.OrdinalIgnoreCase) {
        ["MouseLeft"] = 1, ["MouseRight"] = 2, ["MouseMiddle"] = 4, ["Mouse4"] = 5, ["Mouse5"] = 6,
        ["MouseWheelUp"] = WheelUp, ["MouseWheelDown"] = WheelDown,
        ["Space"] = 0x20, ["Shift"] = 0x10, ["Ctrl"] = 0x11, ["Alt"] = 0xA4, ["LeftAlt"] = 0xA4, ["RightAlt"] = 0xA5, ["Tab"] = 9,
        ["="] = 0xBB, ["Equals"] = 0xBB,
        ["Enter"] = 13, ["Backspace"] = 8, ["Up"] = 0x26, ["Down"] = 0x28, ["Left"] = 0x25, ["Right"] = 0x27,
        ["F1"] = 0x70, ["F2"] = 0x71, ["F3"] = 0x72, ["F4"] = 0x73, ["F5"] = 0x74, ["F6"] = 0x75, ["F7"] = 0x76,
        ["F10"] = 0x79, ["F12"] = 0x7B
    };
    public static readonly string[] Choices = Enumerable.Range('A', 26).Concat(Enumerable.Range('0', 10))
        .Select(k => ((char)k).ToString()).Concat(Special.Keys.Where(k => k is not ("Alt" or "LeftAlt" or "Equals"))).ToArray();
    public static int[] ParseMany(string? keys) => (keys ?? "").Split(',').Select(Parse).ToArray();
    public static string Canonical(string key) => Choices.FirstOrDefault(k => Parse(k) == Parse(key)) ?? key;
    public static void ValidateAction(string? value, HashSet<int> assigned)
    {
        foreach (var name in (value ?? "").Split(',')) {
            int key = Parse(name);
            if (key == 0 || key is 0x1B or 0xA4 or 0x77 or 0x78 or 0x7A)
                throw new InvalidDataException($"'{name.Trim()}' is unsupported or reserved (Esc, Left Alt, F8, F9, F11).");
            if (!assigned.Add(key)) throw new InvalidDataException($"'{name.Trim()}' is assigned more than once.");
        }
    }
    public static int Parse(string? key)
    {
        key = key?.Trim() ?? "";
        if (key.Length == 1 && char.IsAsciiLetterOrDigit(key[0])) return char.ToUpperInvariant(key[0]);
        return Special.GetValueOrDefault(key);
    }
}

public sealed class Settings
{
    public string PhoneBackend { get; set; } = "android";
    public string IPhoneEndpoint { get; set; } = "http://127.0.0.1:8100/";
    public bool AutoConnect { get; set; } = true;
    public int MaxFps { get; set; } = 120;
    public int MaxSize { get; set; } = 1920;
    public int BitrateMbps { get; set; } = 16;
    public bool Audio { get; set; } = true;
    public Profile Profile { get; set; } = Profile.Starter();
    public string GameplayMode { get; set; } = "Auto";
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhoneControl");
    public static string FilePath => Path.Combine(DirectoryPath, "settings.json");
    internal static bool ImportSettingsIfMissing(string previousDirectory, string destinationDirectory)
    {
        string source = Path.Combine(previousDirectory, "settings.json");
        string destination = Path.Combine(destinationDirectory, "settings.json");
        if (File.Exists(destination) || !File.Exists(source)) return false;
        Directory.CreateDirectory(destinationDirectory);
        File.Copy(source, destination, overwrite: false);
        return true;
    }
    public static Settings Load()
    {
        try {
            if (!File.Exists(FilePath)) {
                // The stable installer identity locates settings from a previous
                // product name without keeping obsolete branding in the app.
                using var registration = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{CF1762E2-784A-49DA-AF70-A6753131BE40}_is1");
                if (registration?.GetValue("Inno Setup: App Path") is string installedPath) {
                    string previousName = Path.GetFileName(installedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (!string.IsNullOrWhiteSpace(previousName))
                        ImportSettingsIfMissing(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), previousName), DirectoryPath);
                }
            }
            var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath))!;
            bool upgraded = s.Profile.UpgradeSprintMapping(); s.Profile.Validate(); s.Profile.EnsureDrivingLayout();
            if (upgraded) s.Save();
            return s;
        }
        catch (Exception e) { if (File.Exists(FilePath)) Log.Write("Could not load settings: " + e.Message); return new(); }
    }
    public void Save()
    {
        Profile.EnsureDrivingLayout();
        Profile.Validate();
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(FilePath + ".tmp", FilePath, true);
    }
}

public static class Log
{
    public static bool TraceTouches { get; set; }
    private static readonly object Gate = new();
    public static string PathName => Path.Combine(Settings.DirectoryPath, "phonecontrol.log");
    public static event Action<string>? Message;
    public static void Write(string text)
    {
        lock (Gate) {
            try {
                Directory.CreateDirectory(Settings.DirectoryPath);
                if (File.Exists(PathName) && new FileInfo(PathName).Length > 4_000_000)
                    File.Move(PathName, PathName + ".previous", true);
                File.AppendAllText(PathName, $"{DateTimeOffset.Now:O} {text}{Environment.NewLine}");
            } catch { }
        }
        Message?.Invoke(text);
    }
}
