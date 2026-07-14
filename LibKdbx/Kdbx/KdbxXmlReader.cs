using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace LibKdbx;

/// <summary>
/// Parses the XML payload of a KDBX database. Works with the Phase 1 data model:
/// <see cref="EntryAttributes"/> / <see cref="EntryAttachments"/> instead of
/// DgNet's Dictionary/List, <see cref="TriState"/> instead of bool?, and all new
/// Metadata/Group/Entry fields.
/// </summary>
public class KdbxXmlReader(
    Database db,
    ProtectedStream ps,
    bool isV4,
    IReadOnlyList<BinaryPoolEntry>? binaryPool = null
)
{
    private readonly Database _db = db;
    private readonly ProtectedStream _ps = ps;
    private readonly bool _isV4 = isV4;
    private readonly IReadOnlyList<BinaryPoolEntry> _binaryPool = binaryPool ?? [];

    // Set in ReadFrom: either _binaryPool (V4) or parsed from <Meta><Binaries> (V3)
    private List<BinaryPoolEntry> _pool = [];

    public void ReadFrom(Stream stream)
    {
        var xml = XDocument.Load(stream);
        XElement keePassFile =
            xml.Root ?? throw new InvalidDataException("Missing root XML element.");

        if (_isV4)
        {
            _pool = [.. _binaryPool];
        }
        else
        {
            _pool = ParseMetaBinaries(keePassFile.Element("Meta"));
        }

        Metadata meta = ParseMeta(keePassFile.Element("Meta"));

        XElement rootEl =
            keePassFile.Element("Root")
            ?? throw new InvalidDataException("Missing <Root> element.");

        // Parse DeletedObjects before the group tree
        ParseDeletedObjects(rootEl);

        XElement rootGroupEl =
            rootEl.Element("Group")
            ?? throw new InvalidDataException("Missing <Root><Group> element.");

        Group root = ParseGroup(rootGroupEl);
        _db.SetupLoadedData(meta, root);
    }

    // ── Meta ─────────────────────────────────────────────────────────────

    private Metadata ParseMeta(XElement? el)
    {
        if (el is null)
        {
            return new Metadata();
        }

        XElement? memoryProtection = el.Element("MemoryProtection");

        return new Metadata
        {
            Generator = el.Element("Generator")?.Value ?? "",
            Name = el.Element("DatabaseName")?.Value ?? "",
            NameChanged = ParseDateNullable(el.Element("DatabaseNameChanged")?.Value),
            Description = el.Element("DatabaseDescription")?.Value ?? "",
            DescriptionChanged = ParseDateNullable(el.Element("DatabaseDescriptionChanged")?.Value),
            DefaultUserName = el.Element("DefaultUserName")?.Value ?? "",
            DefaultUserNameChanged = ParseDateNullable(el.Element("DefaultUserNameChanged")?.Value),
            MaintenanceHistoryDays = ParseInt(el.Element("MaintenanceHistoryDays")?.Value, 365),
            Color = el.Element("Color")?.Value ?? "",
            RecycleBinEnabled = ParseBool(el.Element("RecycleBinEnabled")?.Value, true),
            RecycleBinUuid = ParseUuid(el.Element("RecycleBinUUID")?.Value),
            RecycleBinChanged = ParseDateNullable(el.Element("RecycleBinChanged")?.Value),
            HistoryMaxItems = ParseInt(el.Element("HistoryMaxItems")?.Value, 10),
            HistoryMaxSize = ParseLong(el.Element("HistoryMaxSize")?.Value, 6_291_456),
            ProtectTitle = ParseBool(memoryProtection?.Element("ProtectTitle")?.Value, false),
            ProtectUserName = ParseBool(memoryProtection?.Element("ProtectUserName")?.Value, false),
            ProtectPassword = ParseBool(memoryProtection?.Element("ProtectPassword")?.Value, true),
            ProtectUrl = ParseBool(memoryProtection?.Element("ProtectURL")?.Value, false),
            ProtectNotes = ParseBool(memoryProtection?.Element("ProtectNotes")?.Value, false),
            MasterKeyChangeRec = ParseInt(el.Element("MasterKeyChangeRec")?.Value, -1),
            MasterKeyChangeForce = ParseInt(el.Element("MasterKeyChangeForce")?.Value, -1),
            EntryTemplatesGroup = ParseUuid(el.Element("EntryTemplatesGroup")?.Value),
            EntryTemplatesGroupChanged = ParseDateNullable(
                el.Element("EntryTemplatesGroupChanged")?.Value
            ),
            LastSelectedGroup = ParseUuid(el.Element("LastSelectedGroup")?.Value),
            LastTopVisibleGroup = ParseUuid(el.Element("LastTopVisibleGroup")?.Value),
            CustomIcons = ParseCustomIcons(el.Element("CustomIcons")),
            CustomData = ParseCustomData(el.Element("CustomData")),
        };
    }

    // ── Custom icons ─────────────────────────────────────────────────────

    private List<CustomIcon> ParseCustomIcons(XElement? el)
    {
        var list = new List<CustomIcon>();
        if (el is null)
        {
            return list;
        }
        foreach (XElement iconEl in el.Elements("Icon"))
        {
            Guid uuid = ParseUuid(iconEl.Element("UUID")?.Value);
            string? data = iconEl.Element("Data")?.Value;
            if (data is null)
            {
                continue;
            }
            list.Add(
                new CustomIcon
                {
                    Uuid = uuid,
                    Data = Convert.FromBase64String(data),
                    Name = iconEl.Element("Name")?.Value ?? "",
                    LastModificationTime = ParseDateNullable(
                        iconEl.Element("LastModificationTime")?.Value
                    ),
                }
            );
        }
        return list;
    }

    // ── Custom data ──────────────────────────────────────────────────────

    private static CustomData? ParseCustomData(XElement? el)
    {
        if (el is null)
        {
            return null;
        }
        var cd = new CustomData();
        foreach (XElement itemEl in el.Elements("Item"))
        {
            string? key = itemEl.Element("Key")?.Value;
            string? value = itemEl.Element("Value")?.Value;
            if (key is not null && value is not null)
            {
                cd.Set(key, value);
            }
        }
        return cd.Count > 0 ? cd : null;
    }

    // ── Binary pool (KDBX 3.x) ───────────────────────────────────────────

    private static List<BinaryPoolEntry> ParseMetaBinaries(XElement? metaEl)
    {
        var pool = new List<BinaryPoolEntry>();
        XElement? binariesEl = metaEl?.Element("Binaries");
        if (binariesEl is null)
        {
            return pool;
        }

        foreach (XElement el in binariesEl.Elements("Binary"))
        {
            XAttribute? idAttr = el.Attribute("ID");
            if (idAttr is null || !int.TryParse(idAttr.Value, out int id))
            {
                continue;
            }

            byte[] data = Convert.FromBase64String(el.Value);

            bool compressed =
                el.Attribute("Compressed")?.Value.Equals("True", StringComparison.OrdinalIgnoreCase)
                ?? false;
            if (compressed)
            {
                data = Decompress(data);
            }

            while (pool.Count <= id)
            {
                pool.Add(new BinaryPoolEntry(false, []));
            }
            pool[id] = new BinaryPoolEntry(false, data);
        }

        return pool;
    }

    // ── Deleted objects ──────────────────────────────────────────────────

    private void ParseDeletedObjects(XElement rootEl)
    {
        XElement? delEl = rootEl.Element("DeletedObjects");
        if (delEl is null)
        {
            return;
        }

        foreach (XElement objEl in delEl.Elements("DeletedObject"))
        {
            Guid uuid = ParseUuid(objEl.Element("UUID")?.Value);
            DateTime deletionTime = ParseDate(objEl.Element("DeletionTime")?.Value);
            _db.DeletedObjects.Add(new DeletedObject(uuid, deletionTime));
        }
    }

    // ── Group (recursive, document order) ────────────────────────────────

    private Group ParseGroup(XElement el)
    {
        var group = new Group
        {
            Uuid = ParseUuid(el.Element("UUID")?.Value),
            Name = el.Element("Name")?.Value ?? "",
            Notes = el.Element("Notes")?.Value ?? "",
            IconId = ParseInt(el.Element("IconID")?.Value, 0),
            CustomIconUuid = ParseUuid(el.Element("CustomIconUUID")?.Value),
            IsExpanded = ParseBool(el.Element("IsExpanded")?.Value, true),
            EnableAutoType = ParseTriState(el.Element("EnableAutoType")?.Value),
            EnableSearching = ParseTriState(el.Element("EnableSearching")?.Value),
            Tags = el.Element("Tags")?.Value ?? "",
            DefaultAutoTypeSequence = el.Element("DefaultAutoTypeSequence")?.Value ?? "",
            LastTopVisibleEntry = ParseUuid(el.Element("LastTopVisibleEntry")?.Value),
            PreviousParentGroup = ParseUuid(el.Element("PreviousParentGroup")?.Value),
            MergeMode = ParseMergeMode(el.Element("MergeMode")?.Value),
            Times = ParseTimes(el.Element("Times")),
            CustomData = ParseCustomData(el.Element("CustomData")),
        };

        foreach (XElement child in el.Elements())
        {
            if (child.Name == "Entry")
            {
                group.AddEntry(ParseEntry(child));
            }
            else if (child.Name == "Group")
            {
                group.AddGroup(ParseGroup(child));
            }
        }

        return group;
    }

    // ── Entry ────────────────────────────────────────────────────────────

    private Entry ParseEntry(XElement el)
    {
        var entry = new Entry
        {
            Uuid = ParseUuid(el.Element("UUID")?.Value),
            IconId = ParseInt(el.Element("IconID")?.Value, 0),
            CustomIconUuid = ParseUuid(el.Element("CustomIconUUID")?.Value),
            ForegroundColor = el.Element("ForegroundColor")?.Value ?? "",
            BackgroundColor = el.Element("BackgroundColor")?.Value ?? "",
            OverrideUrl = el.Element("OverrideURL")?.Value ?? "",
            Tags = el.Element("Tags")?.Value ?? "",
            Times = ParseTimes(el.Element("Times")),
            AutoType = ParseAutoType(el.Element("AutoType")),
            PreviousParentGroup = ParseUuid(el.Element("PreviousParentGroup")?.Value),
            CustomData = ParseCustomData(el.Element("CustomData")),
        };

        // QualityCheck → ExcludeFromReports (inverted)
        string? qc = el.Element("QualityCheck")?.Value;
        if (qc is not null)
        {
            entry.ExcludeFromReports = !ParseBool(qc, true);
        }

        // Attributes
        ParseEntryStrings(el, entry);

        // Attachments (resolve pool index)
        ParseEntryBinaries(el, entry);

        // History
        XElement? historyEl = el.Element("History");
        if (historyEl is not null)
        {
            foreach (XElement histEntry in historyEl.Elements("Entry"))
            {
                entry.History.Add(ParseEntry(histEntry));
            }
        }

        return entry;
    }

    private void ParseEntryStrings(XElement el, Entry entry)
    {
        foreach (XElement strEl in el.Elements("String"))
        {
            string key = strEl.Element("Key")?.Value ?? "";
            XElement? valEl = strEl.Element("Value");
            if (valEl is null)
            {
                continue;
            }

            bool isProtected =
                valEl
                    .Attribute("Protected")
                    ?.Value.Equals("True", StringComparison.OrdinalIgnoreCase)
                ?? false;

            string value;
            if (isProtected)
            {
                byte[] cipher = Convert.FromBase64String(valEl.Value);
                byte[] plain = _ps.Process(cipher);
                value = Encoding.UTF8.GetString(plain);
            }
            else
            {
                value = valEl.Value;
            }

            entry.Attributes.Set(key, value, isProtected);
        }
    }

    private void ParseEntryBinaries(XElement el, Entry entry)
    {
        foreach (XElement binEl in el.Elements("Binary"))
        {
            string key = binEl.Element("Key")?.Value ?? "";
            XElement? valEl = binEl.Element("Value");
            if (valEl is null)
            {
                continue;
            }

            XAttribute? refAttr = valEl.Attribute("Ref");
            if (refAttr is null || !int.TryParse(refAttr.Value, out int idx))
            {
                continue;
            }
            if (idx < 0 || idx >= _pool.Count)
            {
                continue;
            }

            BinaryPoolEntry poolEntry = _pool[idx];
            byte[] data = poolEntry.Data;
            entry.Attachments.Set(key, data);
        }
    }

    // ── AutoType ─────────────────────────────────────────────────────────

    private static AutoType ParseAutoType(XElement? el)
    {
        if (el is null)
        {
            return new AutoType();
        }
        var at = new AutoType
        {
            Enabled = ParseBool(el.Element("Enabled")?.Value, true),
            DataTransferObfuscation = ParseInt(el.Element("DataTransferObfuscation")?.Value, 0),
            DefaultSequence = el.Element("DefaultSequence")?.Value ?? "",
        };
        foreach (XElement assoc in el.Elements("Association"))
        {
            at.Associations.Add(
                new AutoTypeAssociation
                {
                    Window = assoc.Element("Window")?.Value ?? "",
                    Sequence = assoc.Element("KeystrokeSequence")?.Value ?? "",
                }
            );
        }
        return at;
    }

    // ── Times ────────────────────────────────────────────────────────────

    private Times ParseTimes(XElement? el)
    {
        if (el is null)
        {
            return new Times();
        }
        return new Times
        {
            CreationTime = ParseDate(el.Element("CreationTime")?.Value),
            LastModificationTime = ParseDate(el.Element("LastModificationTime")?.Value),
            LastAccessTime = ParseDate(el.Element("LastAccessTime")?.Value),
            ExpiryTime = ParseDate(el.Element("ExpiryTime")?.Value),
            Expires = ParseBool(el.Element("Expires")?.Value, false),
            UsageCount = ParseInt(el.Element("UsageCount")?.Value, 0),
            LocationChanged = ParseDate(el.Element("LocationChanged")?.Value),
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static readonly DateTime s_kdbxV4Epoch = new(1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private DateTime? ParseDateNullable(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        return ParseDate(value);
    }

    private DateTime ParseDate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return DateTime.MinValue;
        }
        if (_isV4)
        {
            byte[] bytes = Convert.FromBase64String(value);
            long seconds = BinaryPrimitives.ReadInt64LittleEndian(bytes);
            return s_kdbxV4Epoch.AddSeconds(seconds);
        }
        return DateTime.Parse(value, null, DateTimeStyles.RoundtripKind);
    }

    private static Guid ParseUuid(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Guid.Empty;
        }
        try
        {
            return new Guid(Convert.FromBase64String(value));
        }
        catch
        {
            return Guid.Empty;
        }
    }

    private static bool ParseBool(string? value, bool defaultValue) =>
        string.IsNullOrEmpty(value)
            ? defaultValue
            : value.Equals("True", StringComparison.OrdinalIgnoreCase);

    private static int ParseInt(string? value, int defaultValue) =>
        int.TryParse(value, out int n) ? n : defaultValue;

    private static long ParseLong(string? value, long defaultValue) =>
        long.TryParse(value, out long n) ? n : defaultValue;

    private static TriState ParseTriState(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return TriState.Inherit;
        }
        if (value.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return TriState.Inherit;
        }
        return value.Equals("True", StringComparison.OrdinalIgnoreCase)
            ? TriState.Enable
            : TriState.Disable;
    }

    private static MergeMode ParseMergeMode(string? value)
    {
        return value switch
        {
            "KeepNewer" => MergeMode.KeepNewer,
            "Synchronize" => MergeMode.Synchronize,
            _ => MergeMode.Default,
        };
    }

    private static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
