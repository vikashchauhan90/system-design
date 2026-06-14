namespace DistributedSystem.Crdt.Models;


public readonly record struct Timestamp(
    long UnixTimeMilliseconds,
    string NodeId) : IComparable<Timestamp>
{
    public int CompareTo(Timestamp other)
    {
        var timeCompare =
            UnixTimeMilliseconds.CompareTo(other.UnixTimeMilliseconds);

        if (timeCompare != 0)
            return timeCompare;

        return string.Compare(
            NodeId,
            other.NodeId,
            StringComparison.Ordinal);
    }
    public static bool operator >(Timestamp left, Timestamp right)
    => left.CompareTo(right) > 0;

    public static bool operator <(Timestamp left, Timestamp right)
        => left.CompareTo(right) < 0;

    public static bool operator >=(Timestamp left, Timestamp right)
        => left.CompareTo(right) >= 0;

    public static bool operator <=(Timestamp left, Timestamp right)
        => left.CompareTo(right) <= 0;
}
