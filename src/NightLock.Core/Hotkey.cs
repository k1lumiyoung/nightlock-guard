namespace NightLock.Core;

/// <summary>
/// Pure-data model and helpers for the emergency stop hotkey. The actual low-level
/// keyboard hook lives in the session helper (Windows-only); this type only knows
/// virtual-key codes so it can be shared, validated, serialized, and unit-tested on
/// any platform. It never records typed input, only the configured key set.
///
/// @spec spec://modules/core/FEAT-004-emergency-stop-hotkey#hotkey-config
/// @spec spec://modules/core/PROP-001-product-boundaries#safety-boundaries
/// </summary>
public static class Hotkey
{
    // Windows virtual-key codes used by the default combo.
    public const int VkLShift = 0xA0;
    public const int VkRShift = 0xA1;

    /// <summary>Default combo: Left Shift + Right Shift + 6 + 7 held together.</summary>
    public static IReadOnlyList<int> DefaultKeys { get; } = new[] { VkLShift, VkRShift, 0x36, 0x37 };

    // Modifier virtual-key codes (generic + left/right specific). A valid combo needs
    // at least one non-modifier key so it cannot be triggered by modifiers alone.
    private static readonly HashSet<int> Modifiers = new()
    {
        0x10, 0x11, 0x12,             // Shift, Control, Alt (generic)
        0xA0, 0xA1, 0xA2, 0xA3,       // L/R Shift, L/R Control
        0xA4, 0xA5,                   // L/R Alt (menu)
        0x5B, 0x5C                    // L/R Windows
    };

    private static readonly Dictionary<int, string> Names = BuildNameTable();
    private static readonly Dictionary<string, int> Tokens = BuildTokenTable();

    public static bool IsModifier(int vk) => Modifiers.Contains(vk);

    /// <summary>
    /// A combo is valid when it has 2..6 distinct keys and at least one non-modifier.
    /// </summary>
    public static bool IsValid(IReadOnlyList<int>? keys)
    {
        if (keys is null)
        {
            return false;
        }

        var distinct = new HashSet<int>(keys);
        if (distinct.Count < 2 || distinct.Count > 6)
        {
            return false;
        }

        return distinct.Any(vk => !IsModifier(vk));
    }

    /// <summary>Returns the keys (de-duplicated, original order kept) if valid, else the default combo.</summary>
    public static IReadOnlyList<int> NormalizeOrDefault(IReadOnlyList<int>? keys)
    {
        if (!IsValid(keys))
        {
            return DefaultKeys.ToArray();
        }

        var seen = new HashSet<int>();
        var ordered = new List<int>();
        foreach (var vk in keys!)
        {
            if (seen.Add(vk))
            {
                ordered.Add(vk);
            }
        }

        return ordered;
    }

    /// <summary>Human-readable combo, e.g. "Left Shift + Right Shift + 6 + 7".</summary>
    public static string Describe(IReadOnlyList<int> keys)
        => keys.Count == 0 ? "(none)" : string.Join(" + ", keys.Select(Name));

    public static string Name(int vk)
        => Names.TryGetValue(vk, out var name) ? name : $"0x{vk:X2}";

    /// <summary>
    /// Compact, space-free, round-trippable token form, e.g. "LeftShift+RightShift+6+7".
    /// Suitable for an editable text field that <see cref="Parse"/> can read back.
    /// </summary>
    public static string ToTokenString(IReadOnlyList<int> keys)
        => string.Join("+", keys.Select(vk => Name(vk).Replace(" ", string.Empty)));

    /// <summary>
    /// Parses a combo from "+"- or space-separated tokens, e.g. "LShift+RShift+6+7".
    /// Returns null when any token is unknown so the caller can reject bad input.
    /// </summary>
    public static IReadOnlyList<int>? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var parts = text.Split(new[] { '+', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var keys = new List<int>();
        foreach (var part in parts)
        {
            if (!Tokens.TryGetValue(part.ToLowerInvariant(), out var vk))
            {
                return null;
            }

            if (!keys.Contains(vk))
            {
                keys.Add(vk);
            }
        }

        return keys.Count == 0 ? null : keys;
    }

    private static Dictionary<int, string> BuildNameTable()
    {
        var names = new Dictionary<int, string>
        {
            [VkLShift] = "Left Shift",
            [VkRShift] = "Right Shift",
            [0x10] = "Shift",
            [0xA2] = "Left Ctrl",
            [0xA3] = "Right Ctrl",
            [0x11] = "Ctrl",
            [0xA4] = "Left Alt",
            [0xA5] = "Right Alt",
            [0x12] = "Alt",
            [0x5B] = "Left Win",
            [0x5C] = "Right Win",
            [0x20] = "Space",
            [0x0D] = "Enter",
            [0x09] = "Tab"
        };

        for (var c = '0'; c <= '9'; c++)
        {
            names[c] = c.ToString();
        }

        for (var c = 'A'; c <= 'Z'; c++)
        {
            names[c] = c.ToString();
        }

        for (var f = 1; f <= 12; f++)
        {
            names[0x70 + (f - 1)] = $"F{f}";
        }

        return names;
    }

    private static Dictionary<string, int> BuildTokenTable()
    {
        var tokens = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (vk, name) in Names)
        {
            tokens[name.Replace(" ", string.Empty).ToLowerInvariant()] = vk;
        }

        // Friendly aliases.
        tokens["lshift"] = VkLShift;
        tokens["rshift"] = VkRShift;
        tokens["lctrl"] = 0xA2;
        tokens["rctrl"] = 0xA3;
        tokens["lalt"] = 0xA4;
        tokens["ralt"] = 0xA5;
        tokens["lwin"] = 0x5B;
        tokens["rwin"] = 0x5C;
        return tokens;
    }
}
