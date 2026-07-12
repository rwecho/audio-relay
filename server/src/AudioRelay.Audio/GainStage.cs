namespace AudioRelay.Audio;

/// <summary>
/// Applies a target volume gain to PCM samples, smoothly ramping the applied gain toward
/// the target (≈100ms at 48kHz) to avoid pops on start/stop/volume changes. Pure DSP, fully
/// unit-testable. Call <see cref="Reset"/> on Start for a fade-in from silence.
/// </summary>
public sealed class GainStage
{
    private const float FadePerSample = 1f / 4800f; // ~100ms full-range ramp at 48kHz

    private float _target = 1f;
    private float _applied = 0f;

    /// <summary>Target gain (0 = mute, 1 = unity, >1 = boost). Clamped to ≥ 0.</summary>
    public float Volume
    {
        get => _target;
        set => _target = Math.Max(0f, value);
    }

    /// <summary>Currently-applied gain (lags <see cref="Volume"/> while fading).</summary>
    public float Applied => _applied;

    public void Apply(Span<float> samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            if (_applied < _target)
                _applied = Math.Min(_target, _applied + FadePerSample);
            else if (_applied > _target)
                _applied = Math.Max(_target, _applied - FadePerSample);
            samples[i] *= _applied;
        }
    }

    /// <summary>Resets the applied gain to silence so the next samples fade in.</summary>
    public void Reset() => _applied = 0f;
}
