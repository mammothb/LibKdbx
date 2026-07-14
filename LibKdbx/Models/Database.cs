namespace LibKdbx;

/// <summary>
/// Top-level KDBX database. Holds the key, settings, metadata, root group,
/// and deleted objects. Populated by <see cref="Kdbx.KdbxReader"/> or
/// <see cref="Database.Create"/> (Phase 2e).
/// </summary>
public class Database
{
    // Set by Reader/Writer — password/key handling in Phase 2e
    public CompositeKey Key { get; internal set; } = new();
    public Settings? Settings { get; internal set; }
    public Version Version { get; internal set; } = new();

    public Metadata? Metadata { get; internal set; }
    public Group? RootGroup { get; internal set; }

    /// <summary>Permanently deleted objects (UUID + deletion time).</summary>
    public List<DeletedObject> DeletedObjects { get; } = [];

    internal void SetChanged() { }

    internal void IndexEntry(Entry entry) { }
    internal void UnindexEntry(Entry entry) { }
    internal void IndexGroup(Group group) { }
    internal void UnindexGroup(Group group) { }

    internal bool IsRecycleBinEnabled() => Metadata?.RecycleBinEnabled ?? false;

    internal Group GetOrCreateRecycleBin() => RootGroup!; // stub — Phase 2e

    internal void SetupLoadedData(Metadata? meta, Group? root)
    {
        Metadata = meta;
        RootGroup = root;
        root?.SetDatabaseRecursive(this);
    }
}
