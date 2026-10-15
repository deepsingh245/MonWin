using System.Globalization;

namespace MonWin.Linux.Providers;

/// <summary>
/// Read-only view of the Linux pseudo-filesystems (/proc, /sys). Everything is read through
/// this thin wrapper so tests can point <c>root</c> at a temp directory of fixture files, and
/// so a missing or unreadable file (different kernel, container, permissions) is always just
/// "no data" instead of an exception.
/// </summary>
internal sealed class LinuxFiles(string root = "/")
{
    private string Map(string path) =>
        root == "/" ? path : Path.Combine(root, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    public string? ReadText(string path)
    {
        try
        {
            var mapped = Map(path);
            return File.Exists(mapped) ? File.ReadAllText(mapped) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public string? ReadTrimmed(string path) => ReadText(path)?.Trim();

    public ulong? ReadUInt64(string path) =>
        ulong.TryParse(ReadTrimmed(path), NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>Hex values such as sysfs "vendor" files ("0x10de").</summary>
    public int? ReadHex(string path)
    {
        var text = ReadTrimmed(path);
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        return int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    public bool DirectoryExists(string path)
    {
        try { return Directory.Exists(Map(path)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Names (not full paths) of the immediate subdirectories, sorted; empty when unreadable.</summary>
    public IReadOnlyList<string> ListDirectoryNames(string path)
    {
        try
        {
            var mapped = Map(path);
            if (!Directory.Exists(mapped))
            {
                return [];
            }

            // Symlinked entries (sysfs class dirs are all symlinks) still report as directories.
            return Directory.EnumerateDirectories(mapped)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
