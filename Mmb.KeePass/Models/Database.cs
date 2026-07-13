namespace LibKdbx;

/// <summary>
/// Placeholder stub — full implementation in later phase.
/// Provides the minimal surface Group and Entry need to compile.
/// </summary>
public class Database
{
    public Metadata? Metadata { get; internal set; }
    public Group? RootGroup { get; internal set; }

    internal void SetChanged() { }

    internal void IndexEntry(Entry entry) { }

    internal void UnindexEntry(Entry entry) { }

    internal void IndexGroup(Group group) { }

    internal void UnindexGroup(Group group) { }

    internal bool IsRecycleBinEnabled() => Metadata?.RecycleBinEnabled ?? false;

    internal Group GetOrCreateRecycleBin() => RootGroup!; // stub
}
