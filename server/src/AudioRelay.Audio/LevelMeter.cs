namespace AudioRelay.Audio;

/// <summary>
/// Tracks recent audio loudness from interleaved float samples ([-1,1]) for UI level/waveform
/// display. Updated on the real-time capture thread (<see cref="Update"/>); read on the UI thread
/// (<see cref="LatestRms"/>/<see cref="Snapshot"/>). Pure math over a thread-safe scalar + locked
/// ring buffer, so fully unit-testable.
/// </summary>
public sealed class LevelMeter
{
    private readonly float[] _history;
    private readonly int _capacity;
    private int _head;
    private double _latestRms;
    private double _latestPeak;

    public LevelMeter(int capacity = 128)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _history = new float[capacity];
    }

    /// <summary>Root-mean-square of the most recent chunk (roughly "perceived loudness").</summary>
    public double LatestRms => Volatile.Read(ref _latestRms);

    /// <summary>Peak absolute amplitude of the most recent chunk.</summary>
    public double LatestPeak => Volatile.Read(ref _latestPeak);

    /// <summary>Computes RMS + peak over the chunk and pushes the clamped level into the ring buffer.</summary>
    public void Update(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty) return;
        double sumSq = 0, peak = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double v = samples[i];
            double a = Math.Abs(v);
            sumSq += v * v;
            if (a > peak) peak = a;
        }
        double rms = Math.Sqrt(sumSq / samples.Length);
        float level = (float)Math.Clamp(rms, 0, 1); // guard against stray >1 floats
        lock (_history)
        {
            _history[_head] = level;
            _head = (_head + 1) % _capacity;
        }
        Volatile.Write(ref _latestRms, rms);
        Volatile.Write(ref _latestPeak, peak);
    }

    /// <summary>Oldest→newest snapshot of the last <paramref name="count"/> levels (0..1) for waveform rendering.</summary>
    public float[] Snapshot(int count)
    {
        if (count <= 0) return Array.Empty<float>();
        int n = Math.Min(count, _capacity);
        float[] result = new float[n];
        lock (_history)
        {
            int start = (_head - n + _capacity) % _capacity; // oldest of the last n
            for (int i = 0; i < n; i++)
                result[i] = _history[(start + i) % _capacity];
        }
        return result;
    }
}
