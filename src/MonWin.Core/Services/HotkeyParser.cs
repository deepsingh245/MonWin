namespace SystemMonitor.Services;

/// <summary>
/// Parses user-entered shortcuts like "Ctrl+Alt+M" into RegisterHotKey modifier flags and
/// a virtual-key code. Pure, so it can be unit-tested without a window handle.
/// </summary>
public static class HotkeyParser
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;

    private static readonly Dictionary<string, uint> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 0x20,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["End"] = 0x23,
        ["Home"] = 0x24,
        ["Insert"] = 0x2D,
        ["Delete"] = 0x2E,
    };

    /// <summary>
    /// Requires at least one modifier and exactly one key (A–Z, 0–9, F1–F24 or a named key) —
    /// a bare-letter global hotkey would hijack normal typing everywhere.
    /// </summary>
    public static bool TryParse(string? text, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var rawPart in text.Split('+'))
        {
            var part = rawPart.Trim();
            switch (part.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL":
                    modifiers |= ModControl;
                    continue;
                case "ALT":
                    modifiers |= ModAlt;
                    continue;
                case "SHIFT":
                    modifiers |= ModShift;
                    continue;
                case "WIN" or "WINDOWS":
                    modifiers |= ModWin;
                    continue;
            }

            if (virtualKey != 0 || !TryParseKey(part, out virtualKey))
            {
                return false;
            }
        }

        return modifiers != 0 && virtualKey != 0;
    }

    private static bool TryParseKey(string part, out uint virtualKey)
    {
        virtualKey = 0;
        if (part.Length == 1 && char.IsAsciiLetterOrDigit(part[0]))
        {
            // VK codes for A–Z and 0–9 equal their uppercase ASCII values.
            virtualKey = char.ToUpperInvariant(part[0]);
            return true;
        }

        if (part.Length is >= 2 and <= 3 && (part[0] is 'F' or 'f')
            && int.TryParse(part[1..], out var n) && n is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + n - 1); // VK_F1 = 0x70
            return true;
        }

        return NamedKeys.TryGetValue(part, out virtualKey);
    }
}
