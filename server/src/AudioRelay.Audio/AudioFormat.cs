namespace AudioRelay.Audio;

/// <summary>PCM format delivered to the pipeline.</summary>
public sealed record AudioFormat(int SampleRate, int Channels);
