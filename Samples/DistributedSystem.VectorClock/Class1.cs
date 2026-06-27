using System;
using System.Collections.Generic;
using System.Linq;

namespace DistributedSystem.VectorClock;

public sealed record VersionedValue(string Key, string Value, Dictionary<string, int> Clock);

public sealed class VectorClockStore
{
    private readonly Dictionary<string, VersionedValue> _records = new(StringComparer.OrdinalIgnoreCase);

    public VersionedValue? Get(string key)
    {
        return _records.TryGetValue(key, out var existing) ? existing : null;
    }

    public void Put(string key, string value, Dictionary<string, int> clock)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key is required.", nameof(key));
        }

        if (clock is null || clock.Count == 0)
        {
            throw new ArgumentException("Clock is required.", nameof(clock));
        }

        var normalizedClock = Normalize(clock);
        var incoming = new VersionedValue(key, value, normalizedClock);

        if (_records.TryGetValue(key, out var existing))
        {
            if (IsConcurrent(existing.Clock, incoming.Clock))
            {
                _records[key] = Merge(existing, incoming);
            }
            else if (IsNewer(incoming.Clock, existing.Clock))
            {
                _records[key] = incoming;
            }
        }
        else
        {
            _records[key] = incoming;
        }
    }

    public IReadOnlyDictionary<string, VersionedValue> Snapshot()
    {
        return new Dictionary<string, VersionedValue>(_records, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, int> Normalize(Dictionary<string, int> clock)
    {
        return clock.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsNewer(Dictionary<string, int> left, Dictionary<string, int> right)
    {
        var nodes = left.Keys.Union(right.Keys).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var leftGreater = false;

        foreach (var node in nodes)
        {
            var leftValue = left.TryGetValue(node, out var leftCount) ? leftCount : 0;
            var rightValue = right.TryGetValue(node, out var rightCount) ? rightCount : 0;
            if (leftValue < rightValue)
            {
                return false;
            }

            if (leftValue > rightValue)
            {
                leftGreater = true;
            }
        }

        return leftGreater;
    }

    private static bool IsConcurrent(Dictionary<string, int> left, Dictionary<string, int> right)
    {
        return !IsNewer(left, right) && !IsNewer(right, left);
    }

    private static VersionedValue Merge(VersionedValue left, VersionedValue right)
    {
        var mergedClock = left.Clock.Keys.Union(right.Clock.Keys)
            .ToDictionary(node => node, node => Math.Max(left.Clock.GetValueOrDefault(node), right.Clock.GetValueOrDefault(node)), StringComparer.OrdinalIgnoreCase);

        return new VersionedValue(left.Key, $"{left.Value} | {right.Value}", mergedClock);
    }
}
