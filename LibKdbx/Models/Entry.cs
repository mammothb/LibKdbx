namespace LibKdbx;

public class Entry
{
    public Database? Database { get; internal set; }
    public Group? ParentGroup { get; internal set; }

    // ── Identity ──────────────────────────────────────────────────────────

    public Guid Uuid { get; set; } = Guid.NewGuid();
    public int IconId { get; set; }
    public Guid CustomIconUuid { get; set; }
    public string ForegroundColor { get; set; } = "";
    public string BackgroundColor { get; set; } = "";
    public string OverrideUrl { get; set; } = "";
    public string Tags { get; set; } = "";

    // ── New in LibKdbx ────────────────────────────────────────────────

    public CustomData? CustomData { get; set; }
    public Guid PreviousParentGroup { get; set; } // KDBX 4.1
    public bool ExcludeFromReports { get; set; } // KDBX 4.1 <QualityCheck> (inverted)

    // ── Attributes & Attachments (replace DgNet Strings / Binaries) ───────

    public EntryAttributes Attributes { get; set; } = new();
    public EntryAttachments Attachments { get; set; } = new();

    // ── Convenience property delegates ────────────────────────────────────

    public string Title
    {
        get => Attributes.Title;
        set => Attributes.Title = value;
    }

    public string UserName
    {
        get => Attributes.UserName;
        set => Attributes.UserName = value;
    }

    public string Password
    {
        get => Attributes.Password;
        set => Attributes.Password = value;
    }

    public string Url
    {
        get => Attributes.Url;
        set => Attributes.Url = value;
    }

    public string Notes
    {
        get => Attributes.Notes;
        set => Attributes.Notes = value;
    }

    // ── Times & AutoType ──────────────────────────────────────────────────

    public Times Times { get; set; } = Times.Create();
    public AutoType AutoType { get; set; } = new();

    // ── History ───────────────────────────────────────────────────────────

    public List<Entry> History { get; set; } = [];

    // ── CRUD ──────────────────────────────────────────────────────────────

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
            ParentGroup.RemoveEntry(this);
        }
    }

    public void MoveTo(Group group)
    {
        ParentGroup?.RemoveEntry(this);
        group.AddEntry(this);
    }

    public void Update(Action<Entry> update)
    {
        Entry snapshot = DeepCopy();
        update(this);
        History.Add(snapshot);
        TrimHistory();
        Database?.SetChanged();
    }

    public Entry Clone()
    {
        Entry clone = DeepCopy();
        clone.Uuid = Guid.NewGuid();
        clone.History.Clear();
        return clone;
    }

    // ── Private ───────────────────────────────────────────────────────────

    /// <summary>
    /// Full copy preserving UUID. Does not copy Database/ParentGroup references or history.
    /// Used for history snapshots and as base for Clone.
    /// </summary>
    private Entry DeepCopy()
    {
        Entry copy = new()
        {
            Uuid = Uuid,
            IconId = IconId,
            CustomIconUuid = CustomIconUuid,
            ForegroundColor = ForegroundColor,
            BackgroundColor = BackgroundColor,
            OverrideUrl = OverrideUrl,
            Tags = Tags,
            PreviousParentGroup = PreviousParentGroup,
            ExcludeFromReports = ExcludeFromReports,
            Times = Times.Clone(),
            AutoType = AutoType.Clone(),
            Attributes = Attributes.Clone(),
            Attachments = Attachments.Clone(),
        };

        if (CustomData is not null)
        {
            copy.CustomData = new CustomData();
            copy.CustomData.CopyFrom(CustomData);
        }

        // History is intentionally not copied — snapshots must not nest.
        return copy;
    }

    private void TrimHistory()
    {
        int maxItems = Database?.Metadata?.HistoryMaxItems ?? 10;
        long maxSize = Database?.Metadata?.HistoryMaxSize ?? 6_291_456;

        // Trim by count
        while (History.Count > maxItems)
        {
            History.RemoveAt(0);
        }

        // Trim by total size (approximate via attachment data size)
        while (
            History.Count > 0
            && History.Sum(h => h.Attributes.Keys.Count * 128 + h.Attachments.DataSize()) > maxSize
        )
        {
            History.RemoveAt(0);
        }
    }
}
