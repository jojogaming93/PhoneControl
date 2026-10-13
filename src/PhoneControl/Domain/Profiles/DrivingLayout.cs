// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only




namespace PhoneControl.Domain.Profiles;

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
