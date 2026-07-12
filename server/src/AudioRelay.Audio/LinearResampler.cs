namespace AudioRelay.Audio;

/// <summary>
/// Simple, dependency-free linear-interpolation resampler for converting captured system
/// audio to the pipeline's 48kHz rate when the device mix format isn't already 48kHz
/// (rare on modern Windows, which defaults to 48kHz). Pure and unit-testable.
/// </summary>
public static class LinearResampler
{
    public static float[] Resample(float[] input, int inputRate, int outputRate, int channels)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (inputRate <= 0) throw new ArgumentOutOfRangeException(nameof(inputRate));
        if (outputRate <= 0) throw new ArgumentOutOfRangeException(nameof(outputRate));
        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
        if (inputRate == outputRate) return input;

        int inFrames = input.Length / channels;
        if (inFrames == 0) return Array.Empty<float>();

        int outFrames = Math.Max(1, (int)(((long)inFrames * outputRate + inputRate / 2) / inputRate));
        var output = new float[outFrames * channels];
        double ratio = inFrames > 1 ? (double)(inFrames - 1) / (outFrames - 1) : 0;

        for (int o = 0; o < outFrames; o++)
        {
            double pos = o * ratio;
            int i0 = (int)pos;
            double frac = pos - i0;
            int i1 = Math.Min(i0 + 1, inFrames - 1);
            for (int c = 0; c < channels; c++)
                output[o * channels + c] = (float)(input[i0 * channels + c] * (1 - frac) + input[i1 * channels + c] * frac);
        }
        return output;
    }
}
