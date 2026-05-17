using Core;
using NAudio.Wave;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: Cadence.Replay <wavfile> [--threshold N]");
    return 1;
}

var path = args[0];

// --- Load WAV into a mono short[] buffer ---
short[] samples;
int sampleRate;
using (var reader = new AudioFileReader(path))
{
    sampleRate = reader.WaveFormat.SampleRate;
    var channels = reader.WaveFormat.Channels;

    // AudioFileReader normalises everything to float [-1, 1] regardless of
    // source bit depth. Read in chunks and average channels into mono.
    var floatBuffer = new float[8192];
    var monoFloats = new List<float>();
    int read;
    while ((read = reader.Read(floatBuffer, 0, floatBuffer.Length)) > 0)
    {
        for (var i = 0; i < read; i += channels)
        {
            float sum = 0;
            for (var c = 0; c < channels; c++) sum += floatBuffer[i + c];
            monoFloats.Add(sum / channels);
        }
    }

    samples = new short[monoFloats.Count];
    for (var i = 0; i < monoFloats.Count; i++)
    {
        var f = Math.Clamp(monoFloats[i], -1f, 1f);
        samples[i] = (short)(f * short.MaxValue);
    }
}

var durationSec = samples.Length / (double)sampleRate;
Console.WriteLine($"Loaded {samples.Length} samples @ {sampleRate}Hz = {durationSec:F1}s");

var options = new TickDetectorOptions();
// Frame length targeting ~23ms regardless of sample rate, rounded to a power of 2.
var targetFrameLength = (int)Math.Round(sampleRate * 0.023);
var frameLength = 1 << (int)Math.Log2(targetFrameLength);
Console.WriteLine($"Frame length: {frameLength} samples ({frameLength * 1000.0 / sampleRate:F1}ms)");
Console.WriteLine($"Threshold: {options.Threshold}");
Console.WriteLine($"Cooldown: {options.CooldownMs}");

Console.WriteLine();

// --- Wire up the pipeline ---
// Clock is derived from sample position, not wall clock. This makes the run
// deterministic and decouples replay speed from timestamp accuracy.
long sampleIndex = 0;
long Clock() => sampleIndex * 1000L / sampleRate;

var ticks = new List<long>();
var tracker = new CadenceTracker(Clock);
var detector = new TickDetector(
    onTick: t =>
    {
        tracker.RecordTick(t);
        ticks.Add(t);
        Console.WriteLine($"  TICK @ {t,6}ms   rpm={tracker.CurrentRpm,5:F1}");
    },
    clock: Clock,
    options: options);

// --- Replay ---
for (var offset = 0; offset + frameLength <= samples.Length; offset += frameLength)
{
    sampleIndex = offset + frameLength;
    detector.ProcessFrame(samples.AsSpan(offset, frameLength));
}

// --- Summary ---
Console.WriteLine();
Console.WriteLine($"Total ticks: {ticks.Count}");
if (ticks.Count >= 2)
{
    var span = (ticks[^1] - ticks[0]) / 1000.0;
    var avgRpm = (ticks.Count - 1) / span * 60;
    Console.WriteLine($"First tick:  {ticks[0]}ms");
    Console.WriteLine($"Last tick:   {ticks[^1]}ms");
    Console.WriteLine($"Average RPM: {avgRpm:F1}");
}

return 0;