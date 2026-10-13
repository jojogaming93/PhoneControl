// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only




namespace PhoneControl.Domain.Profiles;

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
