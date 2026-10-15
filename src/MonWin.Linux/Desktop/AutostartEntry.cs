using System.Text;

namespace MonWin.Linux.Desktop;

/// <summary>
/// Builds the XDG Autostart (.desktop) file that starts MonWin with the user's session.
/// This is the freedesktop standard honored by GNOME, KDE, Xfce and others; it is a plain
/// per-user file, needs no privileges and is removed again by deleting it.
/// </summary>
internal static class AutostartEntry
{
    public const string FileName = "monwin.desktop";

    public static string Build(IReadOnlyList<string> command)
    {
        var text = new StringBuilder();
        text.Append("[Desktop Entry]\n");
        text.Append("Type=Application\n");
        text.Append("Name=MonWin\n");
        text.Append("Comment=System monitor overlay\n");
        text.Append("Exec=").Append(string.Join(' ', command.Select(QuoteExecArgument))).Append('\n');
        text.Append("Icon=monwin\n");
        text.Append("Terminal=false\n");
        text.Append("Categories=System;Monitor;\n");
        text.Append("X-GNOME-Autostart-enabled=true\n");
        return text.ToString();
    }

    /// <summary>
    /// Quotes one Exec argument per the Desktop Entry spec: wrap in double quotes and
    /// backslash-escape the double quote, backtick and dollar sign; a literal backslash needs
    /// four backslashes (the file-level string escape is applied before the Exec quoting).
    /// A literal percent sign is written as %% so it isn't read as a field code.
    /// </summary>
    internal static string QuoteExecArgument(string argument)
    {
        var quoted = new StringBuilder("\"");
        foreach (var c in argument)
        {
            switch (c)
            {
                case '\\': quoted.Append(@"\\\\"); break;
                case '"': quoted.Append("\\\""); break;
                case '`': quoted.Append("\\`"); break;
                case '$': quoted.Append("\\$"); break;
                case '%': quoted.Append("%%"); break;
                default: quoted.Append(c); break;
            }
        }

        return quoted.Append('"').ToString();
    }
}
