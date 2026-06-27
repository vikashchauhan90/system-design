using System;
using System.Collections.Generic;

namespace DistributedSystem.LamportClock;

public sealed record LamportEvent(string Name, long Timestamp);

public sealed class LamportClock
{
    private long _counter;

    public LamportClock(long initialValue = 0)
    {
        _counter = initialValue;
    }

    public long Tick()
    {
        _counter += 1;
        return _counter;
    }

    public long Merge(long incomingTimestamp)
    {
        _counter = Math.Max(_counter, incomingTimestamp) + 1;
        return _counter;
    }

    public long Current => _counter;
}

public sealed class LamportClockStore
{
    private readonly Dictionary<string, LamportEvent> _events = new(StringComparer.OrdinalIgnoreCase);

    public void Record(string key, long timestamp)
    {
        _events[key] = new LamportEvent(key, timestamp);
    }

    public IReadOnlyDictionary<string, LamportEvent> Snapshot()
    {
        return new Dictionary<string, LamportEvent>(_events, StringComparer.OrdinalIgnoreCase);
    }
}
