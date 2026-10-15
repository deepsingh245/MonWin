namespace SystemMonitor.Services;

/// <summary>The native Windows color dialog (System.Windows.Forms) behind <see cref="IColorPickerService"/>.</summary>
public sealed class WinFormsColorPicker : IColorPickerService
{
    public string? PickColor(string? currentHex)
    {
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
        if (TryParseHex(currentHex, out var r, out var g, out var b))
        {
            dialog.Color = System.Drawing.Color.FromArgb(r, g, b);
        }

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return null;
        }

        var c = dialog.Color;
        return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    private static bool TryParseHex(string? hex, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        var clean = hex.TrimStart('#');
        if (clean.Length != 6)
        {
            return false;
        }

        return byte.TryParse(clean[..2], System.Globalization.NumberStyles.HexNumber, null, out r)
            && byte.TryParse(clean[2..4], System.Globalization.NumberStyles.HexNumber, null, out g)
            && byte.TryParse(clean[4..6], System.Globalization.NumberStyles.HexNumber, null, out b);
    }
}
