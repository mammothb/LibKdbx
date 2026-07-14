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

    internal readonly List<Entry> _history = [];

    /// <summary>Entry history entries (old versions before modification).</summary>
    public IReadOnlyList<Entry> History => _history;

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
        _history.Add(snapshot);
        TrimHistory();
        Database?.SetChanged();
    }

    public Entry Clone()
    {
        Entry clone = DeepCopy();
        clone.Uuid = Guid.NewGuid();
        clone._history.Clear();
        return clone;
    }

    // ── Placeholder resolution ───────────────────────────────────────────

    /// <summary>
    /// Resolves all placeholders (e.g. <c>{TITLE}</c>, <c>{S:attr}</c>) in <paramref name="input"/>.
    /// Delegates to <see cref="PlaceholderResolver.Resolve"/>.
    /// </summary>
    public string ResolvePlaceholder(string input) => PlaceholderResolver.Resolve(this, input);

    // ── EntrySearcher helpers ────────────────────────────────────────────

    /// <summary>True if this entry is inside the recycle bin group.</summary>
    internal bool IsRecycled()
    {
        if (ParentGroup is null || Database is null)
        {
            return false;
        }
        return ParentGroup.Uuid == Database.Metadata?.RecycleBinUuid;
    }

    /// <summary>True if this entry has an expiry date set and expires within <paramref name="days"/>.</summary>
    internal bool WillExpireInDays(int days)
    {
        if (!Times.Expires)
        {
            return false;
        }

        if (Times.ExpiryTime <= DateTime.UtcNow)
        {
            return days == 0;
        }

        return (Times.ExpiryTime - DateTime.UtcNow).TotalDays <= days;
    }

    /// <summary>True if this entry has TOTP configured.</summary>
    internal bool HasTotp()
    {
        return Attributes.Contains("TOTP Seed")
            || Attributes.Contains("TOTP Settings")
            || Attributes.Contains("otp");
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
        while (_history.Count > maxItems)
        {
            _history.RemoveAt(0);
        }

        // Trim by total size (approximate via attachment data size)
        while (
            _history.Count > 0
            && _history.Sum(h => h.Attributes.Keys.Count * 128 + h.Attachments.DataSize()) > maxSize
        )
        {
            _history.RemoveAt(0);
        }
    }
}
