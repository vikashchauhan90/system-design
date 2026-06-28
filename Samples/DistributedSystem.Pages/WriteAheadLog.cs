using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedSystem.Pages;

public sealed class WriteAheadLog
{
    private readonly List<Record> _log = new();

    public void Append(Record record)
    {
        _log.Add(record);
    }

    public IReadOnlyList<Record> Replay() => _log;
}
