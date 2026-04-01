
# Unique Number Generator
```C#
public class LockFreeUniqueNumberGenerator
{
    private long _counter;
    private long _lastTimestamp;
    
    public long GenerateUniqueNumber()
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long currentCounter;
        
        do
        {
            long currentTimestamp = Interlocked.Read(ref _lastTimestamp);
            
            if (timestamp == currentTimestamp)
            {
                currentCounter = Interlocked.Increment(ref _counter);
                if (currentCounter < 1000000) // 1 million per millisecond limit
                {
                    return timestamp * 1000000 + currentCounter;
                }
                // Counter overflow, try with next timestamp
                timestamp = GetNextTimestamp(currentTimestamp);
            }
            else
            {
                // Try to update timestamp
                if (Interlocked.CompareExchange(ref _lastTimestamp, timestamp, currentTimestamp) == currentTimestamp)
                {
                    Interlocked.Exchange(ref _counter, 0);
                    return timestamp * 1000000;
                }
            }
        } while (true);
    }
    
    private long GetNextTimestamp(long currentTimestamp)
    {
        long timestamp;
        do
        {
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        } while (timestamp <= currentTimestamp);
        return timestamp;
    }
}

```

```C#
public class SnowflakeIdGenerator
{
    private readonly long _machineId;
    private readonly long _epoch = 1609459200000L; // 2021-01-01
    private long _lastTimestamp = -1L;
    private long _sequence = 0L;
    private readonly object _lock = new object();
    
    // Bit allocations
    private const int MachineIdBits = 10;
    private const int SequenceBits = 12;
    
    private readonly long _maxMachineId = (1L << MachineIdBits) - 1;
    private readonly long _sequenceMask = (1L << SequenceBits) - 1;
    private readonly int _machineIdShift = SequenceBits;
    private readonly int _timestampShift = SequenceBits + MachineIdBits;
    
    public SnowflakeIdGenerator(long machineId)
    {
        if (machineId < 0 || machineId > _maxMachineId)
        {
            throw new ArgumentException($"Machine ID must be between 0 and {_maxMachineId}");
        }
        _machineId = machineId;
    }
    
    public long GenerateUniqueNumber()
    {
        lock (_lock)
        {
            long timestamp = GetCurrentTimestamp();
            
            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & _sequenceMask;
                
                if (_sequence == 0)
                {
                    // Sequence exhausted, wait for next millisecond
                    while (timestamp <= _lastTimestamp)
                    {
                        timestamp = GetCurrentTimestamp();
                    }
                }
            }
            else
            {
                _sequence = 0;
            }
            
            _lastTimestamp = timestamp;
            
            return ((timestamp - _epoch) << _timestampShift) |
                   (_machineId << _machineIdShift) |
                   _sequence;
        }
    }
    
    private long GetCurrentTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
```