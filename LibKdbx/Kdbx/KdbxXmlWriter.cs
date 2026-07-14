using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace LibKdbx;

/// <summary>
/// Serializes a database's data model to the XML payload of a KDBX file.
/// Uses <see cref="BinaryPoolBuilder"/> for O(N) attachment deduplication
/// instead of DgNet's O(N²) linear scan.
/// Writes all Phase 1 fields: TriState for groups, new Metadata/Group/Entry
/// fields, DeletedObjects, protected attribute flags, etc.
/// </summary>
public class KdbxXmlWriter
{
    private readonly Database _db;
    private readonly ProtectedStream _ps;
    private readonly bool _isV4;
    private readonly List<BinaryPoolEntry> _binaryPool;
    private readonly BinaryPool? _binaryPoolData;

    /// <summary>Binary pool, used by <see cref="KdbxWriter"/> to write the inner header.</summary>
    public IReadOnlyList<BinaryPoolEntry> BinaryPool => _binaryPool;

    public KdbxXmlWriter(Database db, ProtectedStream ps, bool isV4)
    {
        _db = db;
        _ps = ps;
        _isV4 = isV4;
        _binaryPoolData = _db.RootGroup is not null ? BinaryPoolBuilder.Build(_db.RootGroup) : null;
        _binaryPool = BuildBinaryPoolFrom(_binaryPoolData);
    }

    public void WriteTo(Stream stream)
    {
        Metadata meta =
            _db.Metadata ?? throw new InvalidOperationException("Database has no Metadata.");
        Group root =
            _db.RootGroup ?? throw new InvalidOperationException("Database has no RootGroup.");

        var rootEl = new XElement("Root", WriteGroup(root));

        // DeletedObjects
        if (_db.DeletedObjects.Count > 0)
        {
            var delEl = new XElement("DeletedObjects");
            foreach (DeletedObject obj in _db.DeletedObjects)
            {
                delEl.Add(
                    new XElement(
                        "DeletedObject",
                        new XElement("UUID", GuidToBase64(obj.Uuid)),
                        new XElement("DeletionTime", FormatDate(obj.DeletionTime))
                    )
                );
            }
            rootEl.AddFirst(delEl);
        }

        var xml = new XDocument(new XElement("KeePassFile", WriteMeta(meta), rootEl));
        xml.Save(stream);
    }

    // ── Meta ─────────────────────────────────────────────────────────────

    private XElement WriteMeta(Metadata meta)
    {
        var el = new XElement(
            "Meta",
            new XElement("Generator", meta.Generator),
            new XElement("DatabaseName", meta.Name),
            new XElement("DatabaseDescription", meta.Description),
            new XElement("DefaultUserName", meta.DefaultUserName),
            new XElement("RecycleBinEnabled", BoolStr(meta.RecycleBinEnabled)),
            new XElement("RecycleBinUUID", GuidToBase64(meta.RecycleBinUuid)),
            new XElement("HistoryMaxItems", meta.HistoryMaxItems),
            new XElement("HistoryMaxSize", meta.HistoryMaxSize),
            new XElement("MaintenanceHistoryDays", meta.MaintenanceHistoryDays),
            new XElement("MasterKeyChangeRec", meta.MasterKeyChangeRec),
            new XElement("MasterKeyChangeForce", meta.MasterKeyChangeForce),
            new XElement(
                "MemoryProtection",
                new XElement("ProtectTitle", BoolStr(meta.ProtectTitle)),
                new XElement("ProtectUserName", BoolStr(meta.ProtectUserName)),
                new XElement("ProtectPassword", BoolStr(meta.ProtectPassword)),
                new XElement("ProtectURL", BoolStr(meta.ProtectUrl)),
                new XElement("ProtectNotes", BoolStr(meta.ProtectNotes))
            )
        );

        // Optional date fields
        if (meta.NameChanged.HasValue)
        {
            el.Add(new XElement("DatabaseNameChanged", FormatDate(meta.NameChanged.Value)));
        }
        if (meta.DescriptionChanged.HasValue)
        {
            el.Add(
                new XElement(
                    "DatabaseDescriptionChanged",
                    FormatDate(meta.DescriptionChanged.Value)
                )
            );
        }
        if (meta.DefaultUserNameChanged.HasValue)
        {
            el.Add(
                new XElement(
                    "DefaultUserNameChanged",
                    FormatDate(meta.DefaultUserNameChanged.Value)
                )
            );
        }
        if (!string.IsNullOrEmpty(meta.Color))
        {
            el.Add(new XElement("Color", meta.Color));
        }
        if (meta.RecycleBinChanged.HasValue)
        {
            el.Add(new XElement("RecycleBinChanged", FormatDate(meta.RecycleBinChanged.Value)));
        }
        if (meta.EntryTemplatesGroup != Guid.Empty)
        {
            el.Add(new XElement("EntryTemplatesGroup", GuidToBase64(meta.EntryTemplatesGroup)));
        }
        if (meta.EntryTemplatesGroupChanged.HasValue)
        {
            el.Add(
                new XElement(
                    "EntryTemplatesGroupChanged",
                    FormatDate(meta.EntryTemplatesGroupChanged.Value)
                )
            );
        }
        if (meta.LastSelectedGroup != Guid.Empty)
        {
            el.Add(new XElement("LastSelectedGroup", GuidToBase64(meta.LastSelectedGroup)));
        }
        if (meta.LastTopVisibleGroup != Guid.Empty)
        {
            el.Add(new XElement("LastTopVisibleGroup", GuidToBase64(meta.LastTopVisibleGroup)));
        }

        // CustomData
        if (meta.CustomData is { Count: > 0 })
        {
            el.Add(WriteCustomData(meta.CustomData));
        }

        // CustomIcons
        if (meta.CustomIcons.Count > 0)
        {
            var iconsEl = new XElement("CustomIcons");
            foreach (CustomIcon icon in meta.CustomIcons)
            {
                var iconEl = new XElement(
                    "Icon",
                    new XElement("UUID", GuidToBase64(icon.Uuid)),
                    new XElement("Data", Convert.ToBase64String(icon.Data))
                );
                if (!string.IsNullOrEmpty(icon.Name))
                {
                    iconEl.Add(new XElement("Name", icon.Name));
                }
                if (icon.LastModificationTime.HasValue)
                {
                    iconEl.Add(
                        new XElement(
                            "LastModificationTime",
                            FormatDate(icon.LastModificationTime.Value)
                        )
                    );
                }
                iconsEl.Add(iconEl);
            }
            el.Add(iconsEl);
        }

        // V3: binary pool in <Meta><Binaries>
        if (!_isV4 && _binaryPool.Count > 0)
        {
            var binariesEl = new XElement("Binaries");
            for (int i = 0; i < _binaryPool.Count; i++)
            {
                byte[] compressed = CompressGzip(_binaryPool[i].Data);
                binariesEl.Add(
                    new XElement(
                        "Binary",
                        new XAttribute("ID", i),
                        new XAttribute("Compressed", "True"),
                        Convert.ToBase64String(compressed)
                    )
                );
            }
            el.Add(binariesEl);
        }

        return el;
    }

    // ── Group (recursive, entries before sub-groups) ─────────────────────

    private XElement WriteGroup(Group group)
    {
        var el = new XElement(
            "Group",
            new XElement("UUID", GuidToBase64(group.Uuid)),
            new XElement("Name", group.Name),
            new XElement("Notes", group.Notes),
            new XElement("IconID", group.IconId),
            new XElement("IsExpanded", BoolStr(group.IsExpanded)),
            WriteTriState("EnableAutoType", group.EnableAutoType),
            WriteTriState("EnableSearching", group.EnableSearching),
            WriteTimes(group.Times)
        );

        if (group.CustomIconUuid != Guid.Empty)
        {
            el.Add(new XElement("CustomIconUUID", GuidToBase64(group.CustomIconUuid)));
        }
        if (!string.IsNullOrEmpty(group.Tags))
        {
            el.Add(new XElement("Tags", group.Tags));
        }
        if (!string.IsNullOrEmpty(group.DefaultAutoTypeSequence))
        {
            el.Add(new XElement("DefaultAutoTypeSequence", group.DefaultAutoTypeSequence));
        }
        if (group.LastTopVisibleEntry != Guid.Empty)
        {
            el.Add(new XElement("LastTopVisibleEntry", GuidToBase64(group.LastTopVisibleEntry)));
        }
        if (group.PreviousParentGroup != Guid.Empty)
        {
            el.Add(new XElement("PreviousParentGroup", GuidToBase64(group.PreviousParentGroup)));
        }
        if (group.MergeMode != MergeMode.Default)
        {
            string modeName = group.MergeMode switch
            {
                MergeMode.KeepNewer => "KeepNewer",
                MergeMode.Synchronize => "Synchronize",
                _ => "Default",
            };
            el.Add(new XElement("MergeMode", modeName));
        }
        if (group.CustomData is { Count: > 0 })
        {
            el.Add(WriteCustomData(group.CustomData));
        }

        foreach (Entry entry in group.Entries)
        {
            el.Add(WriteEntry(entry));
        }

        foreach (Group sub in group.Groups)
        {
            el.Add(WriteGroup(sub));
        }

        return el;
    }

    // ── Entry ────────────────────────────────────────────────────────────

    private XElement WriteEntry(Entry entry)
    {
        var el = new XElement(
            "Entry",
            new XElement("UUID", GuidToBase64(entry.Uuid)),
            new XElement("IconID", entry.IconId),
            new XElement("ForegroundColor", entry.ForegroundColor),
            new XElement("BackgroundColor", entry.BackgroundColor),
            new XElement("OverrideURL", entry.OverrideUrl),
            new XElement("Tags", entry.Tags),
            WriteTimes(entry.Times)
        );

        if (entry.CustomIconUuid != Guid.Empty)
        {
            el.Add(new XElement("CustomIconUUID", GuidToBase64(entry.CustomIconUuid)));
        }

        // QualityCheck — inverted: ExcludeFromReports=true → "False"
        el.Add(new XElement("QualityCheck", BoolStr(!entry.ExcludeFromReports)));

        if (entry.PreviousParentGroup != Guid.Empty)
        {
            el.Add(new XElement("PreviousParentGroup", GuidToBase64(entry.PreviousParentGroup)));
        }
        if (entry.CustomData is { Count: > 0 })
        {
            el.Add(WriteCustomData(entry.CustomData));
        }

        // Attributes
        foreach (string key in entry.Attributes.Keys)
        {
            if (!entry.Attributes.TryGetValue(key, out string? value))
            {
                continue;
            }

            XElement valEl;
            if (entry.Attributes.IsProtected(key))
            {
                byte[] plain = Encoding.UTF8.GetBytes(value);
                byte[] cipher = _ps.Process(plain);
                valEl = new XElement("Value", Convert.ToBase64String(cipher));
                valEl.SetAttributeValue("Protected", "True");
            }
            else
            {
                valEl = new XElement("Value", value);
            }

            el.Add(new XElement("String", new XElement("Key", key), valEl));
        }

        // Attachments
        foreach (string key in entry.Attachments.Keys)
        {
            if (!entry.Attachments.TryGetValue(key, out byte[]? data))
            {
                continue;
            }
            int idx = GetBinaryIndex(data);
            if (idx < 0)
            {
                continue;
            }
            el.Add(
                new XElement(
                    "Binary",
                    new XElement("Key", key),
                    new XElement("Value", new XAttribute("Ref", idx))
                )
            );
        }

        el.Add(WriteAutoType(entry.AutoType));

        // History
        if (entry.History.Count > 0)
        {
            var historyEl = new XElement("History");
            foreach (Entry hist in entry.History)
            {
                historyEl.Add(WriteEntry(hist));
            }
            el.Add(historyEl);
        }

        return el;
    }

    // ── AutoType ─────────────────────────────────────────────────────────

    private static XElement WriteAutoType(AutoType at)
    {
        var el = new XElement(
            "AutoType",
            new XElement("Enabled", BoolStr(at.Enabled)),
            new XElement("DataTransferObfuscation", at.DataTransferObfuscation),
            new XElement("DefaultSequence", at.DefaultSequence)
        );
        foreach (AutoTypeAssociation assoc in at.Associations)
        {
            el.Add(
                new XElement(
                    "Association",
                    new XElement("Window", assoc.Window),
                    new XElement("KeystrokeSequence", assoc.Sequence)
                )
            );
        }
        return el;
    }

    // ── Times ────────────────────────────────────────────────────────────

    private XElement WriteTimes(Times times) =>
        new(
            "Times",
            new XElement("CreationTime", FormatDate(times.CreationTime)),
            new XElement("LastModificationTime", FormatDate(times.LastModificationTime)),
            new XElement("LastAccessTime", FormatDate(times.LastAccessTime)),
            new XElement("ExpiryTime", FormatDate(times.ExpiryTime)),
            new XElement("Expires", BoolStr(times.Expires)),
            new XElement("UsageCount", times.UsageCount),
            new XElement("LocationChanged", FormatDate(times.LocationChanged))
        );

    // ── CustomData ───────────────────────────────────────────────────────

    private static XElement WriteCustomData(CustomData cd)
    {
        var el = new XElement("CustomData");
        foreach (string key in cd.Keys)
        {
            if (!cd.TryGetValue(key, out string? value) || value is null)
            {
                continue;
            }
            el.Add(new XElement("Item", new XElement("Key", key), new XElement("Value", value)));
        }
        return el;
    }

    // ── TriState ────────────────────────────────────────────────────────

    private static XElement WriteTriState(string elementName, TriState state)
    {
        string value = state switch
        {
            TriState.Enable => "True",
            TriState.Disable => "False",
            _ => "null",
        };
        return new XElement(elementName, value);
    }

    // ── Binary pool ─────────────────────────────────────────────────────

    private static List<BinaryPoolEntry> BuildBinaryPoolFrom(BinaryPool? data)
    {
        var pool = new List<BinaryPoolEntry>();
        if (data is not null)
        {
            foreach (byte[] item in data.Items)
            {
                pool.Add(new BinaryPoolEntry(false, item));
            }
        }

        return pool;
    }

    private int GetBinaryIndex(byte[] data)
    {
        return _binaryPoolData?.GetIndex(data) ?? -1;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static readonly DateTime s_kdbxV4Epoch = new(1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private string FormatDate(DateTime dt)
    {
        if (_isV4)
        {
            long seconds = (long)(dt.ToUniversalTime() - s_kdbxV4Epoch).TotalSeconds;
            byte[] buf = new byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(buf, seconds);
            return Convert.ToBase64String(buf);
        }
        return dt.ToUniversalTime().ToString("O");
    }

    private static string GuidToBase64(Guid g) => Convert.ToBase64String(g.ToByteArray());

    private static string BoolStr(bool b) => b ? "True" : "False";

    private static byte[] CompressGzip(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            gzip.Write(data);
        }
        return output.ToArray();
    }
}
