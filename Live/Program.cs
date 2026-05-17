using System.Diagnostics;
using Ble.Linux;
using Core;

const int SampleRate = 16000;
const int FrameLength = 512;
const double Threshold = 500;
const int CooldownMs = 375;

var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
long Clock() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime;

var tracker = new CadenceTracker(Clock);
var detector = new TickDetector(
    onTick: t =>
    {
        tracker.RecordTick(t);
        Console.WriteLine($"  TICK @ {t,6}ms   rpm={tracker.CurrentRpm,5:F1}");
    },
    clock: Clock,
    options: new TickDetectorOptions { Threshold = Threshold, CooldownMs = CooldownMs });

// Start BLE peripheral. If this fails, fail fast — no point continuing without it.
var peripheral = new CscPeripheral(tracker);
try
{
    await peripheral.StartAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"BLE startup failed: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    return 1;
}

// Spawn arecord. Same as before.
var arecord = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "arecord",
        ArgumentList = { "-D", "plughw:1,0", "-f", "S16_LE", "-c", "1", "-r", SampleRate.ToString(), "-t", "raw", "-q", "-" },
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    },
};

arecord.ErrorDataReceived += (_, e) =>
{
    if (e.Data is not null) Console.Error.WriteLine($"[arecord] {e.Data}");
};

var stopping = false;
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    if (stopping) return;
    stopping = true;
    Console.WriteLine();
    Console.WriteLine("Stopping...");
    try { arecord.Kill(entireProcessTree: true); } catch { }
};

Console.WriteLine($"Capture @ {SampleRate}Hz, frame={FrameLength}, threshold={Threshold}, cooldown={CooldownMs}ms");
Console.WriteLine("Pedal away. Ctrl-C to stop.");
Console.WriteLine();

arecord.Start();
arecord.BeginErrorReadLine();

var byteBuffer = new byte[FrameLength * sizeof(short)];
var shortBuffer = new short[FrameLength];
var stdout = arecord.StandardOutput.BaseStream;

try
{
    while (!stopping)
    {
        await stdout.ReadExactlyAsync(byteBuffer);
        for (var i = 0; i < FrameLength; i++)
            shortBuffer[i] = (short)(byteBuffer[i * 2] | (byteBuffer[i * 2 + 1] << 8));
        detector.ProcessFrame(shortBuffer);
    }
}
catch (EndOfStreamException) { /* arecord exited */ }

await arecord.WaitForExitAsync();
await peripheral.DisposeAsync();
return 0;