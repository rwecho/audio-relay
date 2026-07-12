using Concentus.Enums;
using Concentus.Structs;

namespace AudioRelay.Codec;

/// <summary>
/// Wraps the pure-managed Concentus Opus encoder. Produces one Opus packet per
/// fixed-duration PCM frame, matching the pipeline's real-time cadence (one Encode
/// call per captured frame). Output feeds <see cref="RtpPacketizer"/>.
/// </summary>
public sealed class OpusEncoderAdapter
{
    private const int MaxOpusPacketBytes = 1275;

    private readonly OpusEncoder _encoder;
    private readonly int _channels;
    private readonly int _samplesPerChannel;

    public OpusEncoderAdapter(
        int sampleRateHz = 48000,
        int channels = 2,
        int frameDurationMs = 20,
        int bitrate = 96000)
    {
        _channels = channels;
        _samplesPerChannel = sampleRateHz * frameDurationMs / 1000; // 960 for 48kHz / 20ms

        _encoder = new OpusEncoder(sampleRateHz, channels, OpusApplication.OPUS_APPLICATION_AUDIO)
        {
            Bitrate = bitrate,
            UseDTX = true,
            UseVBR = true
        };
    }

    /// <summary>Samples per channel in one frame (e.g. 960 for 48kHz / 20ms).</summary>
    public int SamplesPerChannel => _samplesPerChannel;

    /// <summary>Total interleaved 16-bit samples expected per Encode call.</summary>
    public int PcmFrameLength => _samplesPerChannel * _channels;

    /// <summary>
    /// Encodes exactly one PCM frame (interleaved 16-bit) into an Opus packet.
    /// </summary>
    public byte[] Encode(ReadOnlySpan<short> pcm)
    {
        if (pcm.Length != PcmFrameLength)
            throw new ArgumentException(
                $"PCM frame must be {PcmFrameLength} samples ({_samplesPerChannel}/ch × {_channels}ch); received {pcm.Length}.",
                nameof(pcm));

        var output = new byte[MaxOpusPacketBytes];
        int encoded = _encoder.Encode(pcm, _samplesPerChannel, output, output.Length);

        if (encoded <= 0)
            throw new InvalidOperationException($"Opus encode returned error code {encoded}.");

        Array.Resize(ref output, encoded);
        return output;
    }
}
