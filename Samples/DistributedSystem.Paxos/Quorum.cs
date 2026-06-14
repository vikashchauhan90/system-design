namespace DistributedSystem.Paxos;

public static class Quorum
{
    public static int Majority(int replicas)
        => replicas / 2 + 1;

    public static int TwoThirds(int replicas)
        => (int)Math.Ceiling(
            replicas * 2d / 3d);
}
