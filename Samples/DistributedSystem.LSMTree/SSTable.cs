using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedSystem.LSMTree;

public class SSTable : IDisposable
{
    private readonly string _filePath;
    private readonly Dictionary<string, long> _index; // key -> file offset
    private FileStream _dataFile;
    private readonly bool _isReadOnly;

    public string FilePath => _filePath;
    public int EntryCount => _index.Count;


    public SSTable(string directory, int sequenceNumber, IEnumerable<KeyValuePair<string, byte[]>> data)
    {
        _filePath = Path.Combine(directory, $"sstable_{sequenceNumber}.dat");
        _index = new Dictionary<string, long>();

        // Write data to disk
        using (var stream = new FileStream(_filePath, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(stream))
        {
            foreach (var kvp in data.OrderBy(x => x.Key))
            {
                // Store position for index
                long position = stream.Position;
                _index[kvp.Key] = position;

                // Write key
                byte[] keyBytes = Encoding.UTF8.GetBytes(kvp.Key);
                writer.Write(keyBytes.Length);
                writer.Write(keyBytes);

                // Write value
                writer.Write(kvp.Value.Length);
                writer.Write(kvp.Value);
            }
        }
        _dataFile = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
        _isReadOnly = true;
    }

    public SSTable(string filePath)
    {
        _filePath = filePath;
        _index = new Dictionary<string, long>();

        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        using (var reader = new BinaryReader(stream))
        {
            while (stream.Position < stream.Length)
            {
                long position = stream.Position;

                // Read key
                int keyLen = reader.ReadInt32();
                byte[] keyBytes = reader.ReadBytes(keyLen);
                string key = Encoding.UTF8.GetString(keyBytes);

                // Read value
                int valueLen = reader.ReadInt32();
                _index[key] = position;

                // Skip value bytes (don't need to read them)
                stream.Seek(valueLen, SeekOrigin.Current);
            }
        }
        _dataFile = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        _isReadOnly = true;
    }

    public byte[]? Get(string key)
    {
        if (!_index.TryGetValue(key, out long position))
            return null;

        lock (_dataFile)
        {
            _dataFile.Seek(position, SeekOrigin.Begin);
            using (var reader = new BinaryReader(_dataFile, Encoding.UTF8, true))
            {
                // Read key (skip)
                int keyLen = reader.ReadInt32();
                _dataFile.Seek(keyLen, SeekOrigin.Current);

                // Read value
                int valueLen = reader.ReadInt32();
                return reader.ReadBytes(valueLen);
            }
        }
    }

    public IEnumerable<KeyValuePair<string, byte[]>> GetAll()
    {
        _dataFile.Seek(0, SeekOrigin.Begin);
        using (var reader = new BinaryReader(_dataFile, Encoding.UTF8, true))
        {
            while (_dataFile.Position < _dataFile.Length)
            {
                // Read key
                int keyLen = reader.ReadInt32();
                byte[] keyBytes = reader.ReadBytes(keyLen);
                string key = Encoding.UTF8.GetString(keyBytes);

                // Read value
                int valueLen = reader.ReadInt32();
                byte[] value = reader.ReadBytes(valueLen);

                yield return new KeyValuePair<string, byte[]>(key, value);
            }
        }
    }


    public void Dispose()
    {
        _dataFile?.Dispose();
    }
}
