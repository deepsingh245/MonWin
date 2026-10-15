using MonWin.Linux.Providers;

namespace MonWin.Linux.Tests;

/// <summary>
/// A temp directory standing in for "/" so /proc and /sys fixtures can be written as ordinary
/// files. Lets the real providers run unchanged on any OS, against exactly the text a Linux
/// kernel produces.
/// </summary>
internal sealed class FixtureRoot : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MonWinLinuxTests_" + Guid.NewGuid().ToString("N"));

    public FixtureRoot() => Directory.CreateDirectory(Path);

    public LinuxFiles Files => new(Path);

    private string Map(string absolutePath) =>
        System.IO.Path.Combine(Path, absolutePath.TrimStart('/').Replace('/', System.IO.Path.DirectorySeparatorChar));

    public void Write(string absolutePath, string content)
    {
        var mapped = Map(absolutePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(mapped)!);
        File.WriteAllText(mapped, content);
    }

    public void MakeDirectory(string absolutePath) => Directory.CreateDirectory(Map(absolutePath));

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { /* best-effort cleanup */ }
    }
}
