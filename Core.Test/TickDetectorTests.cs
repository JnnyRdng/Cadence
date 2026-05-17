namespace Core.Test;

public class TickDetectorTests
{
    private sealed class Harness
    {
        public long NowMs;
        public readonly List<long> Ticks = new();
        public readonly TickDetector Detector;

        public Harness(TickDetectorOptions? options = null)
        {
            // Default to a low threshold so LoudFrame's energy of 10000
            // trivially exceeds it. Tests that care about threshold behaviour
            // can pass their own.
            options ??= new TickDetectorOptions { Threshold = 100 };
            Detector = new TickDetector(t => Ticks.Add(t), () => NowMs, options);
        }

        public static short[] QuietFrame() => new short[512];

        public static short[] LoudFrame(short amplitude = 10_000)
        {
            var frame = new short[512];
            for (var i = 0; i < frame.Length; i++)
                frame[i] = (i % 2 == 0) ? amplitude : (short)-amplitude;
            return frame;
        }
    }

    [Fact]
    public void FiresOnFirstLoudFrame()
    {
        var harness = new Harness();
        harness.NowMs = 1000;

        harness.Detector.ProcessFrame(Harness.LoudFrame());

        harness.Ticks.ShouldBe(new long[] { 1000 });
    }

    [Fact]
    public void FiresOnLeadingEdgeOnly()
    {
        // Five loud frames in rapid succession (no clock advancement between)
        // should fire exactly once — leading-edge behaviour.
        var harness = new Harness();
        harness.NowMs = 1000;

        for (var i = 0; i < 5; i++)
            harness.Detector.ProcessFrame(Harness.LoudFrame());

        harness.Ticks.Count.ShouldBe(1);
        harness.Ticks[0].ShouldBe(1000);
    }

    [Fact]
    public void IgnoresFramesWithinCooldown()
    {
        var harness = new Harness(new TickDetectorOptions
        {
            Threshold = 100,
            CooldownMs = 150,
        });

        harness.NowMs = 1000;
        harness.Detector.ProcessFrame(Harness.LoudFrame());

        harness.NowMs = 1100; // 100ms later, inside cooldown
        harness.Detector.ProcessFrame(Harness.LoudFrame());

        harness.Ticks.Count.ShouldBe(1);
    }

    [Fact]
    public void FiresAgainAfterCooldownExpires()
    {
        var harness = new Harness(new TickDetectorOptions
        {
            Threshold = 100,
            CooldownMs = 150,
        });

        harness.NowMs = 1000;
        harness.Detector.ProcessFrame(Harness.LoudFrame());

        harness.NowMs = 1200; // 200ms later, past cooldown
        harness.Detector.ProcessFrame(Harness.LoudFrame());

        harness.Ticks.ShouldBe(new long[] { 1000, 1200 });
    }

    [Fact]
    public void IgnoresQuietFrames()
    {
        var harness = new Harness();

        harness.NowMs = 1000;
        for (var i = 0; i < 100; i++)
        {
            harness.NowMs += 32;
            harness.Detector.ProcessFrame(Harness.QuietFrame());
        }

        harness.Ticks.ShouldBeEmpty();
    }

    [Fact]
    public void IgnoresFramesBelowThreshold()
    {
        // Threshold 5000, frame energy 1000 → no fire
        var harness = new Harness(new TickDetectorOptions { Threshold = 5000 });

        harness.NowMs = 1000;
        harness.Detector.ProcessFrame(Harness.LoudFrame(amplitude: 1000));

        harness.Ticks.ShouldBeEmpty();
    }

    [Fact]
    public void EmptyFrameDoesNotCrash()
    {
        var harness = new Harness();
        harness.NowMs = 1000;

        harness.Detector.ProcessFrame(ReadOnlySpan<short>.Empty);

        harness.Ticks.ShouldBeEmpty();
    }

    [Fact]
    public void RejectsDoubleClicksFromReedSwitch()
    {
        // Reed switch produces two distinct clicks per revolution: contact
        // closes (click 1) then opens 20-80ms later (click 2). The cooldown
        // must absorb the second click as part of the same physical event.
        var harness = new Harness(new TickDetectorOptions
        {
            Threshold = 100,
            CooldownMs = 150,
        });

        // First revolution
        harness.NowMs = 1000;
        harness.Detector.ProcessFrame(Harness.LoudFrame()); // close
        harness.NowMs = 1050;
        harness.Detector.ProcessFrame(Harness.LoudFrame()); // open, 50ms later

        // Second revolution, ~1 second later (60 RPM)
        harness.NowMs = 2000;
        harness.Detector.ProcessFrame(Harness.LoudFrame()); // close
        harness.NowMs = 2050;
        harness.Detector.ProcessFrame(Harness.LoudFrame()); // open

        harness.Ticks.ShouldBe(new long[] { 1000, 2000 });
    }

    [Fact]
    public void SimulatesRealisticPedalling()
    {
        // 90 RPM = tick every ~667ms. Simulate 4 seconds of pedalling:
        // alternate quiet frames with occasional loud frames at the right interval.
        var harness = new Harness(new TickDetectorOptions
        {
            Threshold = 100,
            CooldownMs = 150,
        });

        const int frameMs = 32;
        long nextTickAt = 0;
        const int tickIntervalMs = 667;

        for (long t = 0; t < 4_000; t += frameMs)
        {
            harness.NowMs = t;
            var frame = (t >= nextTickAt && t < nextTickAt + frameMs)
                ? Harness.LoudFrame()
                : Harness.QuietFrame();

            harness.Detector.ProcessFrame(frame);

            if (t >= nextTickAt) nextTickAt += tickIntervalMs;
        }

        // 4 seconds / 667ms per tick ≈ 6 ticks
        harness.Ticks.Count.ShouldBeInRange(5, 7);
    }
}