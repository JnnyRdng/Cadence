namespace Core;

public sealed class CadenceTracker
{
    private readonly Func<long> _clock;
    private readonly int _rpmWindowMs;
    private readonly object _sync = new();
    private readonly Queue<long> _recentTicks = new();

    private ushort _cumulativeCrankRevolutions;
    private ushort _lastCrankEventTime1024;

    public CadenceTracker(Func<long> clock, int rpmWindowMs = 5000)
    {
        _clock = clock;
        _rpmWindowMs = rpmWindowMs;
    }

    public void RecordTick(long timestampMs)
    {
        lock (_sync)
        {
            _cumulativeCrankRevolutions = unchecked((ushort)(_cumulativeCrankRevolutions + 1));
            _lastCrankEventTime1024 = MsToBleTime(timestampMs);

            _recentTicks.Enqueue(timestampMs);
            PruneOldTicks(_clock());
        }
    }

    public CscSnapshot Snapshot()
    {
        lock (_sync)
        {
            return new CscSnapshot(_cumulativeCrankRevolutions, _lastCrankEventTime1024);
        }
    }

    public double CurrentRpm
    {
        get
        {
            lock (_sync)
            {
                PruneOldTicks(_clock());
                if (_recentTicks.Count < 2) return 0;
                var span = _recentTicks.Last() - _recentTicks.First();
                if (span <= 0) return 0;
                return (_recentTicks.Count - 1) / (double)span * 60_000;
            }
        }
    }

    private void PruneOldTicks(long now)
    {
        var cutoff = now - _rpmWindowMs;
        while (_recentTicks.Count > 0 && _recentTicks.Peek() < cutoff)
            _recentTicks.Dequeue();
    }

    internal static ushort MsToBleTime(long timestampMs) =>
        unchecked((ushort)((timestampMs * 1024L) / 1000L));
}

public readonly record struct CscSnapshot(
    ushort CumulativeCrankRevolutions,
    ushort LastCrankEventTime1024);