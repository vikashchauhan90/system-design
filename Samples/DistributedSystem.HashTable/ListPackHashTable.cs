using System.Text;

namespace DistributedSystem.HashTable;

public class ListPackHashTable
{
    private byte[] _buffer;
    private int _used;
    private int _count;

    public int Count => _count;

    public ListPackHashTable(int initialCapacity = 256)
    {
        _buffer = new byte[initialCapacity];
        _used = 0;
    }

    public byte[]? Get(string key)
    {
        int pos = 0;

        while (pos < _used)
        {
            ReadEntry(pos,
                out string currentKey,
                out byte[] value,
                out int entryLength);

            if (currentKey == key)
                return value;

            pos += entryLength;
        }

        return null;
    }

    public void Add(string key, byte[] value)
    {
        if (TryUpdate(key, value))
            return;

        byte[] keyBytes = Encoding.UTF8.GetBytes(key);

        int entrySize =
            sizeof(int) +
            keyBytes.Length +
            sizeof(int) +
            value.Length;

        EnsureCapacity(_used + entrySize);

        WriteInt(_used, keyBytes.Length);
        _used += sizeof(int);

        Buffer.BlockCopy(
            keyBytes,
            0,
            _buffer,
            _used,
            keyBytes.Length);

        _used += keyBytes.Length;

        WriteInt(_used, value.Length);
        _used += sizeof(int);

        Buffer.BlockCopy(
            value,
            0,
            _buffer,
            _used,
            value.Length);

        _used += value.Length;

        _count++;
    }

    public bool Remove(string key)
    {
        int pos = 0;

        while (pos < _used)
        {
            ReadEntry(pos,
                out string currentKey,
                out _,
                out int entryLength);

            if (currentKey == key)
            {
                int bytesAfter = _used - (pos + entryLength);

                if (bytesAfter > 0)
                {
                    Buffer.BlockCopy(
                        _buffer,
                        pos + entryLength,
                        _buffer,
                        pos,
                        bytesAfter);
                }

                _used -= entryLength;
                _count--;

                return true;
            }

            pos += entryLength;
        }

        return false;
    }

    public IEnumerable<KeyValuePair<string, byte[]>> Entries()
    {
        int pos = 0;

        while (pos < _used)
        {
            ReadEntry(pos,
                out string key,
                out byte[] value,
                out int entryLength);

            yield return new KeyValuePair<string, byte[]>(key, value);

            pos += entryLength;
        }
    }

    private bool TryUpdate(string key, byte[] newValue)
    {
        int pos = 0;

        while (pos < _used)
        {
            int start = pos;

            ReadEntry(pos,
                out string currentKey,
                out byte[] existingValue,
                out int entryLength);

            if (currentKey == key)
            {
                Remove(key);
                Add(key, newValue);
                return true;
            }

            pos = start + entryLength;
        }

        return false;
    }

    private void ReadEntry(
        int position,
        out string key,
        out byte[] value,
        out int totalLength)
    {
        int pos = position;

        int keyLength = ReadInt(pos);
        pos += sizeof(int);

        key = Encoding.UTF8.GetString(
            _buffer,
            pos,
            keyLength);

        pos += keyLength;

        int valueLength = ReadInt(pos);
        pos += sizeof(int);

        value = new byte[valueLength];

        Buffer.BlockCopy(
            _buffer,
            pos,
            value,
            0,
            valueLength);

        pos += valueLength;

        totalLength = pos - position;
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _buffer.Length)
            return;

        int newSize = _buffer.Length * 2;

        while (newSize < required)
            newSize *= 2;

        Array.Resize(ref _buffer, newSize);
    }

    private int ReadInt(int position)
    {
        return BitConverter.ToInt32(_buffer, position);
    }

    private void WriteInt(int position, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);

        Buffer.BlockCopy(
            bytes,
            0,
            _buffer,
            position,
            sizeof(int));
    }
}
