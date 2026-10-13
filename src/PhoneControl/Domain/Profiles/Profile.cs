// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Text.Json;



namespace PhoneControl.Domain.Profiles;

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
            throw new InvalidDataException("Movement radius must be 1â€“30%; sprint reach must be 1â€“45%; sensitivity must be 0.1â€“10.");
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
