namespace AudioRelay.Codec;

/// <summary>Receives a complete 20ms int16 frame (span valid only during the call).</summary>
public delegate void FrameCallback(ReadOnlySpan<short> frame);

/// <summary>
/// Converts a stream of interleaved float samples ([-1, 1]) at 48kHz into exact
/// fixed-size int16 frames for the Opus encoder, buffering partial frames across
/// Feed calls. This is the single framing / format-conversion point in the pipeline;
/// capturing code feeds whatever chunk sizes it receives and this guarantees the
/// encoder always gets exactly <c>samplesPerChannel * channels</c> samples.
/// </summary>
public sealed class FloatToInt16Framer
{
    private readonly int _frameLength;
    private readonly float[] _pending;
    private readonly short[] _frameOut;
    private int _count;

    public FloatToInt16Framer(int samplesPerChannel, int channels)
    {
        if (samplesPerChannel <= 0)
            throw new ArgumentOutOfRangeException(nameof(samplesPerChannel));
        if (channels <= 0)
            throw new ArgumentOutOfRangeException(nameof(channels));

        _frameLength = samplesPerChannel * channels;
        _pending = new float[_frameLength];
        _frameOut = new short[_frameLength];
    }

    /// <summary>Number of float samples buffered, waiting to complete the next frame.</summary>
    public int PendingFloats => _count;

    /// <summary>
    /// Feeds float samples; invokes <paramref name="onFrame"/> once per completed int16 frame.
    /// The span passed to the callback is reused on the next call, so consume it synchronously.
    /// </summary>
    public void Feed(ReadOnlySpan<float> samples, FrameCallback onFrame)
    {
        var input = samples;
        while (true)
        {
            int needed = _frameLength - _count;
            int toCopy = Math.Min(needed, input.Length);
            input.Slice(0, toCopy).CopyTo(_pending.AsSpan(_count, toCopy));
            _count += toCopy;
            input = input.Slice(toCopy);

            if (_count != _frameLength)
                break; // not enough for a full frame yet

            ConvertFrame(_pending.AsSpan(0, _frameLength), _frameOut);
            onFrame(_frameOut);
            _count = 0;

            if (input.Length == 0)
                break;
        }
    }

    private static void ConvertFrame(ReadOnlySpan<float> src, Span<short> dst)
    {
        for (int i = 0; i < src.Length; i++)
            dst[i] = (short)Math.Clamp((int)(src[i] * 32767f), -32768, 32767);
    }
}
