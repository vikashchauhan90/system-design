using DistributedSystem.Sharding.Abstractions;

namespace DistributedSystem.Sharding.RangeSharding;

public sealed class RangeShardRouter<TKey>
    : IShardRouter<TKey>
    where TKey : IComparable<TKey>
{
    private readonly List<RangeShard<TKey, Shard>> _ranges;

    public RangeShardRouter(
        IEnumerable<RangeShard<TKey, Shard>> ranges)
    {
        _ranges = ranges
            .OrderBy(r => r.Start)
            .ToList();

        ValidateRanges(_ranges);
    }

    public Shard GetShard(TKey key)
    {
        var index = BinarySearch(key);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"No shard found for key: {key}");
        }

        return _ranges[index].Shard;
    }

    private int BinarySearch(TKey key)
    {
        int left = 0;
        int right = _ranges.Count - 1;

        while (left <= right)
        {
            int mid = left + (right - left) / 2;

            var range = _ranges[mid];

            if (IsInRange(key, range))
            {
                return mid;
            }

            if (key.CompareTo(range.Start) < 0)
            {
                right = mid - 1;
            }
            else
            {
                left = mid + 1;
            }
        }

        return -1;
    }

    private static bool IsInRange(
        TKey key,
        RangeShard<TKey, Shard> range)
    {
        return key.CompareTo(range.Start) >= 0 &&
               key.CompareTo(range.End) <= 0;
    }

    private static void ValidateRanges(
        List<RangeShard<TKey, Shard>> ranges)
    {
        for (int i = 1; i < ranges.Count; i++)
        {
            var prev = ranges[i - 1];
            var current = ranges[i];

            if (prev.End.CompareTo(current.Start) >= 0)
            {
                throw new InvalidOperationException(
                    $"Overlapping or invalid ranges detected between {prev} and {current}");
            }
        }
    }
}
