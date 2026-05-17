namespace Core;

public sealed class TickDetectorOptions
{
    /// <summary>
    /// Minimum gap between successive ticks, in milliseconds. The reed switch
    /// on the bike will physically bounce for sub-10ms when it closes; at the
    /// other end, pedalling at 120 RPM produces a tick every ~500ms. 150ms is
    /// a safe middle: rejects bounce, comfortably permits realistic cadences.
    /// </summary>
    public int CooldownMs { get; init; } = 400;
    
    /// <summary>
    /// Minimum signal required to register an audio spike as a tick
    /// </summary>
    public double Threshold { get; init; } = 500;
}

public sealed class TickDetector
{
    private readonly Action<long> _onTick;
    private readonly Func<long> _clock;
    private readonly TickDetectorOptions _options;

    private long? _lastTickMs;

    public TickDetector(Action<long> onTick, Func<long> clock, TickDetectorOptions? options = null)
    {
        _onTick = onTick;
        _clock = clock;
        _options = options ?? new TickDetectorOptions();
    }

    /// <summary>
    /// Process one frame of audio. Fires the tick callback if a tick is
    /// detected on the leading edge.
    /// </summary>
    public void ProcessFrame(ReadOnlySpan<short> frame)
    {
        var energy = FrameEnergy(frame);
        if (energy <= _options.Threshold) return;

        var now = _clock();
        if (_lastTickMs.HasValue && now - _lastTickMs.Value < _options.CooldownMs) return;

        _lastTickMs = now;
        _onTick(now);
    }

    private static double FrameEnergy(ReadOnlySpan<short> frame)
    {
        if (frame.Length == 0) return 0;
        long sum = 0;
        foreach (var v in frame)
        {
            sum += v < 0 ? -v : v;
        }

        return (double)sum / frame.Length;
    }
}