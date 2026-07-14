using System.Collections.ObjectModel;

namespace LibKdbx;

public class Group
{
    public Database? Database { get; internal set; }
    public Group? ParentGroup { get; internal set; }

    // ── Core ───────────────────────────────────────────────────────────────

    public Guid Uuid { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Notes { get; set; } = "";
    public int IconId { get; set; }
    public Guid CustomIconUuid { get; set; }
    public bool IsExpanded { get; set; } = true;
    public Times Times { get; set; } = Times.Create();

    // ── TriState flags (was bool? in DgNet) ───────────────────────────────

    public TriState EnableAutoType { get; set; } = TriState.Inherit;
    public TriState EnableSearching { get; set; } = TriState.Inherit;

    // ── New fields in LibKdbx ─────────────────────────────────────────

    public string Tags { get; set; } = "";
    public string DefaultAutoTypeSequence { get; set; } = "";
    public Guid LastTopVisibleEntry { get; set; }
    public Guid PreviousParentGroup { get; set; } // KDBX 4.1
    public MergeMode MergeMode { get; set; } = MergeMode.Default;
    public CustomData? CustomData { get; set; }

    // ── Children ──────────────────────────────────────────────────────────

    private readonly List<Entry> _entries = [];
    private readonly List<Group> _groups = [];

    public ReadOnlyCollection<Entry> Entries => _entries.AsReadOnly();
    public ReadOnlyCollection<Group> Groups => _groups.AsReadOnly();

    // ── CRUD ──────────────────────────────────────────────────────────────

    public void AddEntry(Entry entry)
    {
        if (entry.ParentGroup is not null)
        {
            throw new InvalidOperationException("Entry is already in a group.");
        }

        _entries.Add(entry);
        entry.Database = Database;
        entry.ParentGroup = this;
        if (Database is not null)
        {
            Database.IndexEntry(entry);
            Database.SetChanged();
        }
    }

    public void RemoveEntry(Entry entry)
    {
        if (entry.ParentGroup != this)
        {
            throw new InvalidOperationException("Entry is not in this group.");
        }

        _entries.Remove(entry);
        entry.ParentGroup = null;
        if (Database is not null)
        {
            Database.UnindexEntry(entry);
            entry.Database = null;
            Database.SetChanged();
        }
    }

    public void AddGroup(Group group)
    {
        if (group.ParentGroup is not null)
        {
            throw new InvalidOperationException("Group is already in a group.");
        }

        _groups.Add(group);
        group.ParentGroup = this;
        group.SetDatabaseRecursive(Database);
        if (Database is not null)
        {
            Database.IndexGroup(group);
            Database.SetChanged();
        }
    }

    public void RemoveGroup(Group group)
    {
        if (group.ParentGroup != this)
        {
            throw new InvalidOperationException("Group is not in this group.");
        }

        _groups.Remove(group);
        group.ParentGroup = null;
        if (Database is not null)
        {
            Database.UnindexGroup(group);
            group.SetDatabaseRecursive(null);
            Database.SetChanged();
        }
    }

    // ── Navigation ────────────────────────────────────────────────────────

    public void Delete()
    {
        if (ParentGroup is null)
        {
            return;
        }

        if (Database?.IsRecycleBinEnabled() == true)
        {
            MoveTo(Database.GetOrCreateRecycleBin());
        }
        else
        {
            ParentGroup.RemoveGroup(this);
        }
    }

    public void MoveTo(Group parent)
    {
        if (parent == this)
        {
            throw new InvalidOperationException("Cannot move a group into itself.");
        }

        if (IsAncestorOf(parent))
        {
            throw new InvalidOperationException("Cannot move a group into one of its descendants.");
        }

        ParentGroup?.RemoveGroup(this);
        parent.AddGroup(this);
    }

    public Group Clone()
    {
        Group clone = new()
        {
            Uuid = Guid.NewGuid(),
            Name = Name,
            Notes = Notes,
            IconId = IconId,
            CustomIconUuid = CustomIconUuid,
            IsExpanded = IsExpanded,
            EnableAutoType = EnableAutoType,
            EnableSearching = EnableSearching,
            Tags = Tags,
            DefaultAutoTypeSequence = DefaultAutoTypeSequence,
            LastTopVisibleEntry = LastTopVisibleEntry,
            PreviousParentGroup = PreviousParentGroup,
            MergeMode = MergeMode,
            Times = Times.Clone(),
        };
        if (CustomData is not null)
        {
            clone.CustomData = new CustomData();
            clone.CustomData.CopyFrom(CustomData);
        }

        foreach (Entry entry in Entries)
        {
            clone.AddEntry(entry.Clone());
        }

        foreach (Group group in Groups)
        {
            clone.AddGroup(group.Clone());
        }

        return clone;
    }

    // ── Search ────────────────────────────────────────────────────────────

    public Entry? FindEntry(string title) => FindEntry(e => e.Title == title);

    public Entry? FindEntry(Func<Entry, bool> predicate)
    {
        foreach (Entry entry in _entries)
        {
            if (predicate(entry))
            {
                return entry;
            }
        }

        foreach (Group sub in _groups)
        {
            Entry? found = sub.FindEntry(predicate);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    public IEnumerable<Entry> FindAllEntries(Func<Entry, bool> predicate)
    {
        foreach (Entry entry in _entries)
        {
            if (predicate(entry))
            {
                yield return entry;
            }
        }

        foreach (Group sub in _groups)
        {
            foreach (Entry e in sub.FindAllEntries(predicate))
            {
                yield return e;
            }
        }
    }

    public Group? FindGroup(string name) => FindGroup(g => g.Name == name);

    public Group? FindGroup(Func<Group, bool> predicate)
    {
        foreach (Group sub in _groups)
        {
            if (predicate(sub))
            {
                return sub;
            }

            Group? found = sub.FindGroup(predicate);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    public IEnumerable<Group> FindAllGroups(Func<Group, bool> predicate)
    {
        foreach (Group sub in _groups)
        {
            if (predicate(sub))
            {
                yield return sub;
            }

            foreach (Group g in sub.FindAllGroups(predicate))
            {
                yield return g;
            }
        }
    }

    public bool IsAncestorOf(Group group)
    {
        Group? current = group;
        while (current.ParentGroup is not null)
        {
            if (current == this)
            {
                return true;
            }

            current = current.ParentGroup;
        }

        return false;
    }

    // ── Hierarchy ───────────────────────────────────────────────────────

    /// <summary>Returns this group and all descendant groups.</summary>
    internal List<Group> GroupsRecursive(bool includeSelf = true)
    {
        List<Group> result = [];
        if (includeSelf)
        {
            result.Add(this);
        }
        foreach (Group sub in _groups)
        {
            result.AddRange(sub.GroupsRecursive(true));
        }
        return result;
    }

    /// <summary>Returns the full hierarchy path as list of group names from root to this group.</summary>
    internal List<string> Hierarchy()
    {
        List<string> path = [];
        Group? current = this;
        while (current is not null)
        {
            path.Insert(0, current.Name);
            current = current.ParentGroup;
        }
        return path;
    }

    /// <summary>
    /// Resolves the effective EnableSearching flag by walking up the hierarchy.
    /// Returns false only if a parent has EnableSearching = Disable (not Inherit).
    /// </summary>
    internal bool ResolveSearchingEnabled()
    {
        Group? current = this;
        while (current is not null)
        {
            if (current.EnableSearching == TriState.Disable)
            {
                return false;
            }
            if (current.EnableSearching == TriState.Enable)
            {
                return true;
            }
            current = current.ParentGroup;
        }
        return true; // default
    }

    // ── Internal ──────────────────────────────────────────────────────────

    internal void SetDatabaseRecursive(Database? db)
    {
        Database = db;
        foreach (Entry entry in _entries)
        {
            entry.Database = db;
        }

        foreach (Group sub in _groups)
        {
            sub.SetDatabaseRecursive(db);
        }
    }
}
