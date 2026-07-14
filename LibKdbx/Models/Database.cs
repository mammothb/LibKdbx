namespace LibKdbx;

public class Database : IDisposable
{
    public FileInfo? FileInfo { get; private set; }
    public Metadata? Metadata { get; internal set; }
    public Group? RootGroup { get; internal set; }
    public Settings Settings { get; set; } = new();
    public Version Version { get; internal set; } = new();
    public bool HasChanges { get; private set; }

    private CompositeKey _key = new();
    internal CompositeKey Key
    {
        get => _key;
        set
        {
            _key.Dispose();
            _key = value;
        }
    }

    private readonly Dictionary<Guid, Entry> _entryIndex = [];

    internal readonly List<DeletedObject> _deletedObjects = [];

    /// <summary>Permanently deleted objects (UUID + deletion time).</summary>
    public IReadOnlyList<DeletedObject> DeletedObjects => _deletedObjects;

    // ── Constructors ──────────────────────────────────────────────────────

    public Database() { }

    public Database(CompositeKey key)
    {
        _key = key;
    }

    public Database(string path)
    {
        FileInfo = new FileInfo(path);
    }

    public Database(string path, CompositeKey key)
    {
        FileInfo = new FileInfo(path);
        _key = key;
    }

    public Database(string path, string password)
    {
        FileInfo = new FileInfo(path);
        _key = new CompositeKey(password);
    }

    public Database(string path, string password, string keyFile)
    {
        FileInfo = new FileInfo(path);
        _key = new CompositeKey(password, keyFile);
    }

    // ── Factories ─────────────────────────────────────────────────────────

    public static Database Create(string password, Settings? settings = null)
    {
        Settings s = settings ?? new Settings();
        Database db = new(new CompositeKey(password))
        {
            Settings = s,
            Version = s.Format == KdbxFormat.Kdbx4 ? new Version(4, 1) : new Version(3, 1),
            Metadata = new Metadata(),
            RootGroup = new Group { Name = "Root" },
        };
        db.RootGroup.SetDatabaseRecursive(db);
        return db;
    }

    public static Database Create(string password, string keyFile, Settings? settings = null)
    {
        Settings s = settings ?? new Settings();
        Database db = new(new CompositeKey(password, keyFile))
        {
            Settings = s,
            Version = s.Format == KdbxFormat.Kdbx4 ? new Version(4, 1) : new Version(3, 1),
            Metadata = new Metadata(),
            RootGroup = new Group { Name = "Root" },
        };
        db.RootGroup.SetDatabaseRecursive(db);
        return db;
    }

    // ── Open / Save ───────────────────────────────────────────────────────

    public static Database Open(string path, string password, string? keyFile = null)
    {
        Database db = keyFile is not null
            ? new Database(path, password, keyFile)
            : new Database(path, password);
        db.Open();
        return db;
    }

    /// <summary>
    /// Opens a database file asynchronously.
    /// The <paramref name="ct"/> governs file read I/O only.
    /// KDF derivation, decryption, and XML parsing run synchronously after
    /// the file bytes are loaded into memory.
    /// </summary>
    public static async Task<Database> OpenAsync(
        string path,
        string password,
        string? keyFile = null,
        CancellationToken ct = default
    )
    {
        Database db = keyFile is not null
            ? new Database(path, password, keyFile)
            : new Database(path, password);
        await db.OpenAsync(ct);
        return db;
    }

    public void Open()
    {
        // Delegate to async — safe because File.ReadAllBytesAsync has no
        // SynchronizationContext affinity on thread-pool / console callers.
        OpenAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Opens the database file asynchronously.
    /// The <paramref name="ct"/> governs file read I/O only.
    /// </summary>
    public async Task OpenAsync(CancellationToken ct = default)
    {
        if (FileInfo is null)
        {
            throw new InvalidOperationException("No file path set.");
        }

        byte[] bytes = await File.ReadAllBytesAsync(FileInfo.FullName, ct);
        await using MemoryStream ms = new(bytes);
        new KdbxReader(this).ReadFrom(ms);
        HasChanges = false;
    }

    public void Save()
    {
        SaveAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Saves the database to its file asynchronously.
    /// The <paramref name="ct"/> governs file write I/O only.
    /// XML serialization, encryption, and KDF derivation run synchronously
    /// before the bytes are written.
    /// </summary>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (FileInfo is null)
        {
            throw new InvalidOperationException("No file path set.");
        }

        await using MemoryStream ms = new();
        new KdbxWriter(this).WriteTo(ms);
        await File.WriteAllBytesAsync(FileInfo.FullName, ms.ToArray(), ct);
        HasChanges = false;
    }

    public void SaveAs(string path)
    {
        FileInfo = new FileInfo(path);
        Save();
    }

    /// <summary>
    /// Changes the file path and saves asynchronously.
    /// </summary>
    public async Task SaveAsAsync(string path, CancellationToken ct = default)
    {
        FileInfo = new FileInfo(path);
        await SaveAsync(ct);
    }

    // ── IDisposable ───────────────────────────────────────────────────────

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _key.Dispose();
            Metadata = null;
            RootGroup = null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    // ── Index management ──────────────────────────────────────────────────

    internal void SetChanged() => HasChanges = true;

    internal void IndexEntry(Entry entry)
    {
        _entryIndex[entry.Uuid] = entry;
    }

    internal void UnindexEntry(Entry entry)
    {
        _entryIndex.Remove(entry.Uuid);
    }

    internal void IndexGroup(Group group)
    {
        foreach (Entry entry in group.Entries)
        {
            _entryIndex[entry.Uuid] = entry;
        }
        foreach (Group sub in group.Groups)
        {
            IndexGroup(sub);
        }
    }

    internal void UnindexGroup(Group group)
    {
        foreach (Entry entry in group.Entries)
        {
            _entryIndex.Remove(entry.Uuid);
        }
        foreach (Group sub in group.Groups)
        {
            UnindexGroup(sub);
        }
    }

    // ── Search ────────────────────────────────────────────────────────────

    public Entry? FindEntry(string title) => RootGroup?.FindEntry(title);

    public Entry? FindEntry(Func<Entry, bool> predicate) => RootGroup?.FindEntry(predicate);

    public IEnumerable<Entry> FindAllEntries(Func<Entry, bool> predicate) =>
        RootGroup?.FindAllEntries(predicate) ?? [];

    public Group? FindGroup(string name) => RootGroup?.FindGroup(name);

    public Group? FindGroup(Func<Group, bool> predicate) => RootGroup?.FindGroup(predicate);

    public IEnumerable<Group> FindAllGroups(Func<Group, bool> predicate) =>
        RootGroup?.FindAllGroups(predicate) ?? [];

    public Entry? FindEntryByUuid(Guid uuid) =>
        _entryIndex.TryGetValue(uuid, out Entry? entry) ? entry : null;

    // ── Recycle bin ───────────────────────────────────────────────────────

    public bool IsRecycleBinEnabled() => Metadata?.RecycleBinEnabled ?? false;

    public Group? GetRecycleBin()
    {
        if (!IsRecycleBinEnabled() || Metadata?.RecycleBinUuid == Guid.Empty)
        {
            return null;
        }
        return FindGroup(Metadata!.RecycleBinUuid, RootGroup);
    }

    internal Group GetOrCreateRecycleBin()
    {
        if (RootGroup is null)
        {
            throw new InvalidOperationException("Database has no root group.");
        }

        Group? bin = FindGroup(Metadata?.RecycleBinUuid ?? Guid.Empty, RootGroup);
        if (bin is not null)
        {
            return bin;
        }

        bin = new Group { Name = "Recycle Bin", IsExpanded = false };
        RootGroup.AddGroup(bin);
        Metadata!.RecycleBinUuid = bin.Uuid;
        return bin;
    }

    // ── Load wiring ───────────────────────────────────────────────────────

    internal void SetupLoadedData(Metadata? meta, Group? root)
    {
        Metadata = meta;
        RootGroup = root;
        _entryIndex.Clear();
        if (root is not null)
        {
            WireDatabase(root);
        }
    }

    private void WireDatabase(Group group)
    {
        group.SetDatabaseRecursive(this);
        foreach (Entry entry in group.Entries)
        {
            _entryIndex[entry.Uuid] = entry;
        }
        foreach (Group sub in group.Groups)
        {
            WireDatabase(sub);
        }
    }

    // ── Reference resolution ──────────────────────────────────────────────

    internal string ResolveField(Entry entry, string fieldName, int maxDepth = 10)
    {
        string? value = entry.Attributes.Get(fieldName) ?? "";
        return ResolveValue(value, maxDepth);
    }

    private string ResolveValue(string value, int depth)
    {
        if (depth <= 0 || !FieldReference.TryParse(value, out FieldReference refInfo))
        {
            return value;
        }

        Entry? target = FindReferencedEntry(refInfo);
        if (target is null)
        {
            return value;
        }

        // WantedField 'I' returns the target entry's UUID as a hex string
        if (refInfo.WantedField == 'I')
        {
            return target.Uuid.ToString("N");
        }

        string? fieldKey = FieldReference.FieldCodeToKey(refInfo.WantedField);
        if (fieldKey is null)
        {
            return value;
        }

        if (!target.Attributes.TryGetValue(fieldKey, out string? resolved))
        {
            resolved = "";
        }
        return ResolveValue(resolved, depth - 1);
    }

    private Entry? FindReferencedEntry(FieldReference refInfo)
    {
        if (refInfo.SearchIn == 'I')
        {
            if (GuidRfc4122.TryParseHex(refInfo.SearchValue, out Guid uuid))
            {
                return _entryIndex.GetValueOrDefault(uuid);
            }
            return null;
        }

        if (refInfo.SearchIn == 'O')
        {
            return _entryIndex.Values.FirstOrDefault(e =>
                e.Attributes.ContainsValue(refInfo.SearchValue)
            );
        }

        string? fieldKey = FieldReference.FieldCodeToKey(refInfo.SearchIn);
        if (fieldKey is null)
        {
            return null;
        }

        return _entryIndex.Values.FirstOrDefault(e =>
            e.Attributes.Get(fieldKey) == refInfo.SearchValue
        );
    }

    private static Group? FindGroup(Guid uuid, Group? root)
    {
        if (root is null)
        {
            return null;
        }
        if (root.Uuid == uuid)
        {
            return root;
        }
        foreach (Group sub in root.Groups)
        {
            Group? found = FindGroup(uuid, sub);
            if (found is not null)
            {
                return found;
            }
        }
        return null;
    }
}
