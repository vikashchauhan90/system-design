using System.Text;

namespace DistributedSystem.LSMTree;

internal sealed class CommitLog : IDisposable
{
    private readonly string _filePath;
    private readonly FileStream _stream;
    private readonly BinaryWriter _writer;
    private bool _disposed;

    public CommitLog(string filePath)
    {
        _filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        _stream = new FileStream(_filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        _stream.Seek(0, SeekOrigin.End);
        _writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);
    }

    public void Append(string key, byte[] value)
    {
        ThrowIfDisposed();
        _stream.Seek(0, SeekOrigin.End);
        _writer.Write(key);
        _writer.Write(value.Length);
        _writer.Write(value);
        _writer.Flush();
    }

    public IReadOnlyList<KeyValuePair<string, byte[]>> ReadEntries()
    {
        ThrowIfDisposed();
        var entries = new List<KeyValuePair<string, byte[]>>();
        _stream.Position = 0;
        using var reader = new BinaryReader(_stream, Encoding.UTF8, leaveOpen: true);

        while (_stream.Position < _stream.Length)
        {
            var key = reader.ReadString();
            var valueLength = reader.ReadInt32();
            var value = reader.ReadBytes(valueLength);
            entries.Add(new KeyValuePair<string, byte[]>(key, value));
        }

        _stream.Position = _stream.Length;
        return entries;
    }

    public void Reset()
    {
        ThrowIfDisposed();
        _stream.SetLength(0);
        _stream.Position = 0;
        _writer.Flush();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writer.Dispose();
        _stream.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CommitLog));
        }
    }
}
