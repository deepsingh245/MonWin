namespace SystemMonitor.Services;

/// <summary>
/// Fixed-capacity circular buffer of doubles for sparkline history. Adding beyond
/// capacity silently drops the oldest sample. Resizing (e.g. when the user changes the
/// History setting) rebuilds the buffer, keeping as many of the most recent samples as
/// fit in the new capacity.
/// </summary>
public sealed class RollingHistory
{
    private double[] _buffer;
    private int _start;
    private int _count;

    public RollingHistory(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at least 1.");
        }

        _buffer = new double[capacity];
    }

    public int Capacity => _buffer.Length;
    public int Count => _count;

    public void Add(double value)
    {
        var writeIndex = (_start + _count) % _buffer.Length;
        _buffer[writeIndex] = value;

        if (_count < _buffer.Length)
        {
            _count++;
        }
        else
        {
            _start = (_start + 1) % _buffer.Length;
        }
    }

    public double[] ToArray()
    {
        var result = new double[_count];
        for (var i = 0; i < _count; i++)
        {
            result[i] = _buffer[(_start + i) % _buffer.Length];
        }

        return result;
    }

    /// <summary>Rebuilds this history with a new capacity, preserving the most recent samples.</summary>
    public void Resize(int newCapacity)
    {
        if (newCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(newCapacity), "Capacity must be at least 1.");
        }

        var existing = ToArray();
        _buffer = new double[newCapacity];
        _start = 0;
        _count = 0;

        var keepFrom = Math.Max(0, existing.Length - newCapacity);
        for (var i = keepFrom; i < existing.Length; i++)
        {
            Add(existing[i]);
        }
    }
}
