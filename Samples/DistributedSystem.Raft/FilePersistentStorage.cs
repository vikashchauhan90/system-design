using System.Text.Json;

namespace DistributedSystem.Raft;

public class FilePersistentStorage : IPersistentStorage
{
    private readonly string _folder;
    private readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public FilePersistentStorage(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(_folder);
    }

    public async Task SaveAsync(string nodeId, PersistentState state)
    {
        var path = Path.Combine(_folder, nodeId + ".json");
        var tempPath = Path.Combine(path, ".temp");

        if (!File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }
        using var fs = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(fs, state, _opts);

        // move file
        File.Move(tempPath, path, overwrite: true);
    }

    public async Task<PersistentState?> LoadAsync(string nodeId)
    {
        var path = Path.Combine(_folder, nodeId + ".json");
        if (!File.Exists(path)) return null;
        using var fs = File.OpenRead(path);
        try
        {
            var state = await JsonSerializer.DeserializeAsync<PersistentState>(fs);
            return state;
        }
        catch
        {
            return null;
        }
    }
}
