namespace DistributedSystem.Paxos.Models;

public readonly record struct BallotNumber(
    long Number,
    string NodeId)
    : IComparable<BallotNumber>
{
    public int CompareTo(BallotNumber other)
    {
        var result = Number.CompareTo(other.Number);

        if (result != 0)
            return result;

        return string.Compare(
            NodeId,
            other.NodeId,
            StringComparison.Ordinal);
    }

    public static bool operator >(
        BallotNumber left,
        BallotNumber right)
        => left.CompareTo(right) > 0;

    public static bool operator <(
        BallotNumber left,
        BallotNumber right)
        => left.CompareTo(right) < 0;
}
