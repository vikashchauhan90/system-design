
namespace DistributedSystem.ISAM;

public sealed class SearchResult
{
    public bool Found { get; }

    public Record? Record { get; }

    public int Steps { get; }

    public string Location { get; }

    private SearchResult(bool found, Record? record, int steps, string location)
    {
        Found = found;
        Record = record;
        Steps = steps;
        Location = location;
    }

    public static SearchResult Success(Record record, int steps, string location)
        => new(true, record, steps, location);

    public static SearchResult NotFound(int steps, string location)
        => new(false, null, steps, location);

    public override string ToString()
        => Found
            ? $"FOUND {Record} in {Location} (steps={Steps})"
            : $"NOT FOUND (steps={Steps}, last={Location})";
}
