namespace Mmb.KeePass;

public class Metadata
{
    // ── Identification ─────────────────────────────────────────────────────

    /// <summary>App name that generated this database. Defaults to "Mmb.KeePass".</summary>
    public string Generator { get; set; } = "Mmb.KeePass";

    public string Name { get; set; } = "";
    public DateTime? NameChanged { get; set; }
    public string Description { get; set; } = "";
    public DateTime? DescriptionChanged { get; set; }
    public string DefaultUserName { get; set; } = "";
    public DateTime? DefaultUserNameChanged { get; set; }
    public int MaintenanceHistoryDays { get; set; } = 365;
    public string Color { get; set; } = "";

    // ── Recycle bin ────────────────────────────────────────────────────────

    public bool RecycleBinEnabled { get; set; } = true;
    public Guid RecycleBinUuid { get; set; }
    public DateTime? RecycleBinChanged { get; set; }

    // ── History ────────────────────────────────────────────────────────────

    public int HistoryMaxItems { get; set; } = 10;
    public long HistoryMaxSize { get; set; } = 6_291_456; // 6 MiB

    // ── Memory protection ──────────────────────────────────────────────────

    public bool ProtectTitle { get; set; }
    public bool ProtectUserName { get; set; }
    public bool ProtectPassword { get; set; } = true;
    public bool ProtectUrl { get; set; }
    public bool ProtectNotes { get; set; }

    // ── Master key change policy ───────────────────────────────────────────

    public int MasterKeyChangeRec { get; set; } = -1;
    public int MasterKeyChangeForce { get; set; } = -1;

    // ── Group references (stored as UUIDs) ─────────────────────────────────

    public Guid EntryTemplatesGroup { get; set; }
    public DateTime? EntryTemplatesGroupChanged { get; set; }
    public Guid LastSelectedGroup { get; set; }
    public Guid LastTopVisibleGroup { get; set; }

    // ── Custom icons ───────────────────────────────────────────────────────

    public List<CustomIcon> CustomIcons { get; set; } = [];

    // ── Custom data ────────────────────────────────────────────────────────

    /// <summary>Plugin-defined key-value metadata. Lazy — null when empty.</summary>
    public CustomData? CustomData { get; set; }
}
