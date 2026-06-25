namespace DistributedSystem.HashTable;

internal class Entity
{
    public required string Key;
    public byte[]? Value;
    public Entity? next;
}
