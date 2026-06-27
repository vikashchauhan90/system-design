using System.Text.Json;
using System.Text.Json.Serialization;

namespace DistributedSystem.Raft;

public class FilePersistentStorage : IPersistentStorage
{
    private readonly string _folder;
    private readonly JsonSerializerOptions _opts;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public FilePersistentStorage(string folder)
    {
        _folder = folder ?? throw new ArgumentNullException(nameof(folder));
        Directory.CreateDirectory(_folder);

        _opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        _opts.Converters.Add(new JsonStringEnumConverter());
    }

    public async Task SaveAsync(string nodeId, PersistentState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentNullException.ThrowIfNull(state);

        await _fileLock.WaitAsync();
        try
        {
            var path = Path.Combine(_folder, $"{nodeId}.json");
            var tempPath = Path.Combine(_folder, $"{nodeId}.json.tmp");

            Directory.CreateDirectory(_folder);

            await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(fs, state, _opts);
                await fs.FlushAsync();
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<PersistentState?> LoadAsync(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        await _fileLock.WaitAsync();
        try
        {
            var path = Path.Combine(_folder, $"{nodeId}.json");
            if (!File.Exists(path))
            {
                return null;
            }

            await using var fs = File.OpenRead(path);
            try
            {
                var state = await JsonSerializer.DeserializeAsync<PersistentState>(fs, _opts);
                if (state is null)
                {
                    return new PersistentState();
                }

                state.Log ??= new List<LogEntry>();
                state.State = state.State == default ? NodeState.Follower : state.State;
                return state;
            }
            catch (JsonException)
            {
                return null;
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
