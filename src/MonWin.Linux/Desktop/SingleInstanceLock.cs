namespace MonWin.Linux.Desktop;

/// <summary>
/// Ensures one MonWin per user session by holding an exclusive lock on a file. The OS drops
/// the lock when the process exits (even after a crash), so no stale-lock cleanup is needed.
/// </summary>
internal sealed class SingleInstanceLock : IDisposable
{
    private readonly FileStream? _stream;

    private SingleInstanceLock(FileStream? stream) => _stream = stream;

    /// <summary>Returns null when another instance already holds the lock.</summary>
    public static SingleInstanceLock? TryAcquire(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var stream = new FileStream(
                Path.Combine(directory, "monwin.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new SingleInstanceLock(stream);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // The lock file can't be created at all (read-only directory): it is better to
            // run without single-instance protection than to refuse to start.
            return new SingleInstanceLock(null);
        }
    }

    public void Dispose() => _stream?.Dispose();
}
