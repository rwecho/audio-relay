using AudioRelay.Codec;
using Concentus.Structs;

namespace AudioRelay.Codec.Tests;

public class OpusEncoderAdapterTests
{
    private const int SampleRate = 48000;
    private const int Channels = 2;
    private const int SamplesPerChannel = 960; // 48kHz * 20ms

    [Fact]
    public void Defaults_ReportCorrectFrameDimensions()
    {
        var enc = new OpusEncoderAdapter();

        Assert.Equal(960, enc.SamplesPerChannel);
        Assert.Equal(1920, enc.PcmFrameLength); // 960/ch * 2ch interleaved
    }

    [Fact]
    public void Encode_ReturnsNonEmptyBoundedPacket_ForValidFrame()
    {
        var enc = new OpusEncoderAdapter();
        var pcm = MakeSine(SamplesPerChannel, Channels, 440.0, SampleRate, amplitude: 0.3);

        var packet = enc.Encode(pcm);

        Assert.InRange(packet.Length, 1, 1275);
    }

    [Fact]
    public void Encode_ThrowsOnWrongFrameSize()
    {
        var enc = new OpusEncoderAdapter();
        var tooShort = new short[100];

        Assert.Throws<ArgumentException>(() => enc.Encode(tooShort));
    }

    [Fact]
    public void Encode_RoundTripsToAudibleDecodableAudio()
    {
        // Encoded frames must decode back to signal-carrying audio (not silence/garbage).
        // We warm the encoder/decoder across a few frames and check the strongest carries energy,
        // robust to Opus encoder lookahead.
        var enc = new OpusEncoderAdapter();
        var decoder = new OpusDecoder(SampleRate, Channels);
        var pcm = MakeSine(SamplesPerChannel, Channels, 440.0, SampleRate, amplitude: 0.3);

        double maxRms = 0;
        for (int i = 0; i < 5; i++)
        {
            var packet = enc.Encode(pcm);
            var decoded = new short[SamplesPerChannel * Channels];
            int n = decoder.Decode(packet, decoded, SamplesPerChannel, decode_fec: false);

            Assert.Equal(SamplesPerChannel, n);
            maxRms = Math.Max(maxRms, Rms(decoded));
        }

        Assert.True(maxRms > 1000, $"decoded audio should carry strong signal energy, maxRMS={maxRms:F0}");
    }

    [Fact]
    public void Encode_ConsecutiveFrames_AllProduceValidPackets()
    {
        var enc = new OpusEncoderAdapter();
        var pcm = MakeSine(SamplesPerChannel, Channels, 440.0, SampleRate, amplitude: 0.3);

        for (int i = 0; i < 3; i++)
        {
            var packet = enc.Encode(pcm);
            Assert.InRange(packet.Length, 1, 1275);
        }
    }

    private static short[] MakeSine(int samplesPerChannel, int channels, double freqHz, int sampleRate, double amplitude)
    {
        var pcm = new short[samplesPerChannel * channels];
        for (int i = 0; i < samplesPerChannel; i++)
        {
            short v = (short)(amplitude * short.MaxValue * Math.Sin(2 * Math.PI * freqHz * i / sampleRate));
            for (int c = 0; c < channels; c++)
                pcm[i * channels + c] = v;
        }
        return pcm;
    }

    private static double Rms(short[] samples)
    {
        double sum = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double x = samples[i];
            sum += x * x;
        }
        return Math.Sqrt(sum / samples.Length);
    }
}
