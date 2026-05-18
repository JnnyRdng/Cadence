using Android.Media;

namespace Mobile;

/// <summary>
/// Wraps Android's AudioRecord to capture mono 16-bit PCM samples.
/// Caller polls <see cref="ReadFrame"/> in a loop on a background thread.
/// </summary>
public sealed class AndroidAudioSource : IDisposable
{
    public int SampleRate { get; }
    public int FrameLength { get; }

    private readonly AudioRecord _record;
    private readonly short[] _buffer;

    public AndroidAudioSource(int sampleRate = 44100, int frameLength = 1024)
    {
        SampleRate = sampleRate;
        FrameLength = frameLength;
        _buffer = new short[FrameLength];
        
        // AudioRecord wants a buffer at least as big as the minimum reported
        // by the OS. Multiply by 2 for some headroom; small buffers risk
        // overruns on busy schedulers.
        var minBufBytes = AudioRecord.GetMinBufferSize(
            sampleRate, ChannelIn.Mono, Encoding.Pcm16bit);
        var bufBytes = Math.Max(minBufBytes * 2, frameLength * sizeof(short) * 4);

        // AudioSource.Mic uses whichever input the system considers primary.
        // When a USB audio device is connected and granted permission, Android
        // routes Mic through it automatically.
        _record = new AudioRecord(
            AudioSource.Mic,
            sampleRate,
            ChannelIn.Mono,
            Encoding.Pcm16bit,
            bufBytes);

        if (_record.State != State.Initialized)
            throw new InvalidOperationException(
                $"AudioRecord failed to initialise (state={_record.State}). " +
                "Check RECORD_AUDIO permission and that an input device is available.");
    }
    
    public void Start() => _record.StartRecording();

    public void Stop() => _record.Stop();

    /// <summary>
    /// Blocks until a full frame's worth of samples is available.
    /// Returns the number of samples read (== FrameLength on success, 0 on stop).
    /// </summary>
    public int ReadFrame(short[] destination)
    {
        if (destination.Length < FrameLength)
            throw new ArgumentException("Destination buffer too small.", nameof(destination));

        var read = _record.Read(destination, 0, FrameLength);
        return read > 0 ? read : 0;
    }

    public void Dispose()
    {
        try { _record.Stop(); } catch { /* may already be stopped */ }
        _record.Release();
        _record.Dispose();
    }
}