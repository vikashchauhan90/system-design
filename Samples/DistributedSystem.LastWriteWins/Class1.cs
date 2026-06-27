using System;
using System.Collections.Generic;

namespace DistributedSystem.LastWriteWins;

public sealed record RecordValue(string Key, string Value, long Version);

public sealed class LastWriteWinsStore
{
    private readonly Dictionary<string, RecordValue> _records = new(StringComparer.OrdinalIgnoreCase);

    public RecordValue? Get(string key)
    {
        return _records.TryGetValue(key, out var existing) ? existing : null;
    }

    public void Put(string key, string value, long version)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key is required.", nameof(key));
        }

        if (version < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        var incoming = new RecordValue(key, value, version);

        if (_records.TryGetValue(key, out var existing))
        {
            if (incoming.Version >= existing.Version)
            {
                _records[key] = incoming;
            }
        }
        else
        {
            _records[key] = incoming;
        }
    }

    public IReadOnlyDictionary<string, RecordValue> Snapshot()
    {
        return new Dictionary<string, RecordValue>(_records, StringComparer.OrdinalIgnoreCase);
    }
}
