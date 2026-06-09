using System.Text;

namespace DistributedSystem.WAL;

public sealed record WalEntry(long SequenceNumber, DateTime Timestamp, byte[] Payload)
{
    public string PayloadText => Encoding.UTF8.GetString(Payload);
}
