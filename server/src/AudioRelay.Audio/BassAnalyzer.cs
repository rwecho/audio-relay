namespace AudioRelay.Audio;

/// <summary>
/// Streaming bass-band energy estimator for the rhythm-particle effect. Accumulates mono samples
/// (downmixed from interleaved input), runs a Hann-windowed 1024-point radix-2 FFT per block, sums
/// the low-frequency bins (~46-234 Hz at 48 kHz), and exposes a smoothed 0..1 "bass impact". Pure
/// math over a volatile scalar; updated on the capture thread, read on the UI thread — fully unit-
/// testable.
/// </summary>
public sealed class BassAnalyzer
{
    private const int FftSize = 1024;
    private const int BassBins = 5;   // bins 1..5 ≈ 46-234 Hz at 48 kHz / 1024
    private const double Decay = 0.92; // per-block peak-hold decay (~visual release)
    private const double Sensitivity = 0.25; // higher = reacts to quieter bass

    private readonly int _channels;
    private readonly double[] _window;
    private readonly double[] _re = new double[FftSize];
    private readonly double[] _im = new double[FftSize];
    private readonly double[] _mono = new double[FftSize];
    private int _filled;
    private double _bass;

    public BassAnalyzer(int channels = 2)
    {
        _channels = channels <= 0 ? 1 : channels;
        _window = new double[FftSize];
        for (int i = 0; i < FftSize; i++)
            _window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (FftSize - 1)); // Hann window
    }

    /// <summary>Smoothed bass-band energy, 0..1. Safe to read from any thread.</summary>
    public double LatestBass => Volatile.Read(ref _bass);

    /// <summary>Feed interleaved float samples; a fresh bass estimate is produced every FftSize mono samples.</summary>
    public void Update(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty) return;
        int ch = _channels;
        for (int i = 0; i < samples.Length; i += ch)
        {
            double sum = 0;
            int taken = 0;
            for (int c = 0; c < ch && i + c < samples.Length; c++) { sum += samples[i + c]; taken++; }
            _mono[_filled++] = taken > 0 ? sum / taken : 0;
            if (_filled >= FftSize) { Compute(); _filled = 0; }
        }
    }

    private void Compute()
    {
        for (int i = 0; i < FftSize; i++) { _re[i] = _mono[i] * _window[i]; _im[i] = 0; }
        Fft(_re, _im);

        double energy = 0;
        for (int k = 1; k <= BassBins; k++) // skip DC (bin 0)
            energy += Math.Sqrt(_re[k] * _re[k] + _im[k] * _im[k]);

        // A full-scale sine lands ~FftSize/2 per bin; normalize across the bass band.
        double norm = Math.Clamp(energy / ((FftSize / 2.0) * BassBins * Sensitivity), 0, 1);
        double prev = Volatile.Read(ref _bass);
        Volatile.Write(ref _bass, Math.Max(norm, prev * Decay)); // peak-hold with release
    }

    /// <summary>Iterative in-place radix-2 Cooley-Tukey FFT. Length must be a power of two.</summary>
    private static void Fft(double[] re, double[] im)
    {
        int n = re.Length;
        for (int i = 1, j = 0; i < n; i++) // bit-reversal permutation
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            double ang = -2 * Math.PI / len;
            double wr = Math.Cos(ang), wi = Math.Sin(ang);
            int half = len >> 1;
            for (int i = 0; i < n; i += len)
            {
                double cr = 1, ci = 0;
                for (int k = 0; k < half; k++)
                {
                    double tr = cr * re[i + k + half] - ci * im[i + k + half];
                    double ti = cr * im[i + k + half] + ci * re[i + k + half];
                    re[i + k + half] = re[i + k] - tr;
                    im[i + k + half] = im[i + k] - ti;
                    re[i + k] += tr;
                    im[i + k] += ti;
                    double ncr = cr * wr - ci * wi;
                    ci = cr * wi + ci * wr;
                    cr = ncr;
                }
            }
        }
    }
}
