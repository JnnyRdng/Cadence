namespace Core.Test;

public class CadenceTrackerTests
{
    // A simple controllable clock for tests
    private sealed class FakeClock
    {
        public long NowMs;
        public long Read() => NowMs;
    }

    [Fact]
    public void StartsAtZero()
    {
        var clock = new FakeClock();
        var tracker = new CadenceTracker(clock.Read);
        var snap = tracker.Snapshot();
        snap.CumulativeCrankRevolutions.ShouldBe((ushort)0);
        snap.LastCrankEventTime1024.ShouldBe((ushort)0);
    }

    [Fact]
    public void IncrementsRevolutionCountPerTick()
    {
        var clock = new FakeClock();
        var tracker = new CadenceTracker(clock.Read);

        tracker.RecordTick(1000);
        tracker.RecordTick(2000);
        tracker.RecordTick(3000);

        tracker.Snapshot().CumulativeCrankRevolutions.ShouldBe((ushort)3);
    }

    [Fact]
    public void StoresLastEventTimeInBleUnits()
    {
        var clock = new FakeClock();
        var tracker = new CadenceTracker(clock.Read);

        tracker.RecordTick(1000); // 1s → 1024 in BLE units

        tracker.Snapshot().LastCrankEventTime1024.ShouldBe((ushort)1024);
    }

    [Fact]
    public void WrapsRevolutionCounterAtUInt16Boundary()
    {
        var clock = new FakeClock();
        var tracker = new CadenceTracker(clock.Read);

        for (var i = 0; i < 65_537; i++)
            tracker.RecordTick(i);

        // After 65,537 ticks, counter should have wrapped: 65537 mod 65536 = 1
        tracker.Snapshot().CumulativeCrankRevolutions.ShouldBe((ushort)1);
    }

    [Fact]
    public void WrapsEventTimeAtUInt16Boundary()
    {
        var clock = new FakeClock();
        var tracker = new CadenceTracker(clock.Read);

        // 64,000 ms × 1024 / 1000 = 65,536, wraps to 0
        tracker.RecordTick(64_000);

        tracker.Snapshot().LastCrankEventTime1024.ShouldBe((ushort)0);
    }

    [Fact]
    public void RpmIsZeroWithFewerThanTwoTicks()
    {
        var clock = new FakeClock { NowMs = 10_000 };
        var tracker = new CadenceTracker(clock.Read);

        tracker.CurrentRpm.ShouldBe(0);

        tracker.RecordTick(9_500);
        tracker.CurrentRpm.ShouldBe(0);
    }

    [Fact]
    public void RpmReflectsTickRate()
    {
        // Ticks every 500ms = 2 per second = 120 RPM
        var clock = new FakeClock();
        var tracker = new CadenceTracker(clock.Read);

        for (var t = 0; t <= 2_000; t += 500)
        {
            clock.NowMs = t;
            tracker.RecordTick(t);
        }

        // 5 ticks across 2000ms = 4 intervals × 500ms apart = 120 RPM
        tracker.CurrentRpm.ShouldBe(120, tolerance: 0.01);
    }

    [Fact]
    public void RpmDropsToZeroAfterTicksAgeOut()
    {
        var clock = new FakeClock();
        var tracker = new CadenceTracker(clock.Read, rpmWindowMs: 5_000);

        clock.NowMs = 0;
        tracker.RecordTick(0);
        tracker.RecordTick(500);

        clock.NowMs = 10_000; // 10s later, well past the 5s window
        tracker.CurrentRpm.ShouldBe(0);
    }

    [Fact]
    public void SnapshotDoesNotChangeWhenNoNewTicks()
    {
        // Verifies the spec-correct behaviour: if no tick happened since the
        // last notification, send the same values so the watch sees zero delta.
        var clock = new FakeClock();
        var tracker = new CadenceTracker(clock.Read);

        tracker.RecordTick(1000);
        var first = tracker.Snapshot();

        clock.NowMs = 5_000;
        var second = tracker.Snapshot();

        second.ShouldBe(first);
    }
}