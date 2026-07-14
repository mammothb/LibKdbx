using System.Security.Cryptography;
using System.Xml.Linq;

namespace LibKdbx.Tests;

public class KdbxXmlReaderTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static (Database db, ProtectedStream ps) SetupV4()
    {
        Database db = new() { Key = new CompositeKey("test") };
        byte[] psKey = RandomNumberGenerator.GetBytes(64);
        ProtectedStream ps = new(ProtectedStreamAlgorithm.ChaCha20, psKey);
        return (db, ps);
    }

    private static KdbxXmlReader CreateReader(Database db, ProtectedStream ps, bool isV4 = true) =>
        new(db, ps, isV4);

    private static void FeedXml(KdbxXmlReader reader, XDocument doc)
    {
        using var ms = new MemoryStream();
        doc.Save(ms);
        ms.Position = 0;
        reader.ReadFrom(ms);
    }

    private static XDocument MinimalKeePassDoc(XElement meta, XElement rootGroup)
    {
        return new XDocument(new XElement("KeePassFile", meta, new XElement("Root", rootGroup)));
    }

    private static XElement MinimalGroup(string name = "Root")
    {
        return new XElement(
            "Group",
            new XElement("UUID", Convert.ToBase64String(Guid.NewGuid().ToByteArray())),
            new XElement("Name", name),
            new XElement(
                "Times",
                new XElement("CreationTime", Convert.ToBase64String(new byte[8])),
                new XElement("LastModificationTime", Convert.ToBase64String(new byte[8])),
                new XElement("LastAccessTime", Convert.ToBase64String(new byte[8])),
                new XElement("ExpiryTime", Convert.ToBase64String(new byte[8])),
                new XElement("Expires", "False"),
                new XElement("UsageCount", "0"),
                new XElement("LocationChanged", Convert.ToBase64String(new byte[8]))
            )
        );
    }

    private static XElement MinimalMeta() =>
        new(
            "Meta",
            new XElement("Generator", "TestGen"),
            new XElement("DatabaseName", "TestDB"),
            new XElement("RecycleBinEnabled", "True"),
            new XElement("RecycleBinUUID", Convert.ToBase64String(Guid.NewGuid().ToByteArray())),
            new XElement("HistoryMaxItems", "10"),
            new XElement("HistoryMaxSize", "6291456"),
            new XElement("MaintenanceHistoryDays", "365"),
            new XElement("MasterKeyChangeRec", "-1"),
            new XElement("MasterKeyChangeForce", "-1"),
            new XElement(
                "MemoryProtection",
                new XElement("ProtectTitle", "False"),
                new XElement("ProtectUserName", "False"),
                new XElement("ProtectPassword", "True"),
                new XElement("ProtectURL", "False"),
                new XElement("ProtectNotes", "False")
            )
        );

    // ── ParseMeta null element ───────────────────────────────────────────

    [Fact]
    public void ParseMeta_Null_Element_Returns_Defaults()
    {
        (Database db, ProtectedStream ps) = SetupV4();
        KdbxXmlReader reader = CreateReader(db, ps);
        XDocument doc = MinimalKeePassDoc(
            new XElement("Meta"), // present but empty
            MinimalGroup()
        );

        FeedXml(reader, doc);

        db.Metadata!.Generator.ShouldBe("");
        db.Metadata.Name.ShouldBe("");
        db.Metadata.HistoryMaxItems.ShouldBe(10);
    }

    // ── DeletedObjects ──────────────────────────────────────────────────

    [Fact]
    public void DeletedObjects_Parsed_Correctly()
    {
        (Database db, ProtectedStream ps) = SetupV4();
        KdbxXmlReader reader = CreateReader(db, ps);
        Guid delUuid = Guid.NewGuid();

        XElement meta = MinimalMeta();
        XElement rootEl = new("Root", MinimalGroup());
        rootEl.AddFirst(
            new XElement(
                "DeletedObjects",
                new XElement(
                    "DeletedObject",
                    new XElement("UUID", Convert.ToBase64String(delUuid.ToByteArray())),
                    new XElement("DeletionTime", Convert.ToBase64String(new byte[8]))
                )
            )
        );

        XDocument doc = new(new XElement("KeePassFile", meta, rootEl));
        FeedXml(reader, doc);

        db.DeletedObjects.Count.ShouldBe(1);
        db.DeletedObjects[0].Uuid.ShouldBe(delUuid);
    }

    [Fact]
    public void DeletedObjects_Multiple()
    {
        (Database db, ProtectedStream ps) = SetupV4();
        KdbxXmlReader reader = CreateReader(db, ps);

        XElement meta = MinimalMeta();
        XElement rootEl = new("Root", MinimalGroup());
        rootEl.AddFirst(
            new XElement(
                "DeletedObjects",
                new XElement(
                    "DeletedObject",
                    new XElement("UUID", Convert.ToBase64String(Guid.NewGuid().ToByteArray())),
                    new XElement("DeletionTime", Convert.ToBase64String(new byte[8]))
                ),
                new XElement(
                    "DeletedObject",
                    new XElement("UUID", Convert.ToBase64String(Guid.NewGuid().ToByteArray())),
                    new XElement("DeletionTime", Convert.ToBase64String(new byte[8]))
                )
            )
        );

        XDocument doc = new(new XElement("KeePassFile", meta, rootEl));
        FeedXml(reader, doc);

        db.DeletedObjects.Count.ShouldBe(2);
    }

    // ── ParseUuid invalid base64 ────────────────────────────────────────

    [Fact]
    public void ParseUuid_Invalid_Base64_Returns_Empty()
    {
        (Database db, ProtectedStream ps) = SetupV4();
        KdbxXmlReader reader = CreateReader(db, ps);

        XElement group = new(
            "Group",
            new XElement("UUID", "!!!not-valid-base64!!!"),
            new XElement("Name", "BadUuid"),
            new XElement("Times", new XElement("CreationTime", Convert.ToBase64String(new byte[8])))
        );

        XDocument doc = MinimalKeePassDoc(MinimalMeta(), group);
        FeedXml(reader, doc);

        db.RootGroup!.Uuid.ShouldBe(Guid.Empty);
        db.RootGroup.Name.ShouldBe("BadUuid");
    }

    // ── ParseTriState ───────────────────────────────────────────────────

    [Fact]
    public void ParseTriState_Null_String_Returns_Inherit()
    {
        (Database db, ProtectedStream ps) = SetupV4();
        KdbxXmlReader reader = CreateReader(db, ps);

        XElement group = MinimalGroup();
        group.Add(new XElement("EnableAutoType", "null"));
        group.Add(new XElement("EnableSearching", ""));

        XDocument doc = MinimalKeePassDoc(MinimalMeta(), group);
        FeedXml(reader, doc);

        db.RootGroup!.EnableAutoType.ShouldBe(TriState.Inherit);
        db.RootGroup.EnableSearching.ShouldBe(TriState.Inherit);
    }

    [Fact]
    public void ParseTriState_False_Returns_Disable()
    {
        (Database db, ProtectedStream ps) = SetupV4();
        KdbxXmlReader reader = CreateReader(db, ps);

        XElement group = MinimalGroup();
        group.Add(new XElement("EnableAutoType", "False"));

        XDocument doc = MinimalKeePassDoc(MinimalMeta(), group);
        FeedXml(reader, doc);

        db.RootGroup!.EnableAutoType.ShouldBe(TriState.Disable);
    }

    // ── ParseMergeMode ──────────────────────────────────────────────────

    [Fact]
    public void ParseMergeMode_Synchronize()
    {
        (Database db, ProtectedStream ps) = SetupV4();
        KdbxXmlReader reader = CreateReader(db, ps);

        XElement group = MinimalGroup();
        group.Add(new XElement("MergeMode", "Synchronize"));

        XDocument doc = MinimalKeePassDoc(MinimalMeta(), group);
        FeedXml(reader, doc);

        db.RootGroup!.MergeMode.ShouldBe(MergeMode.Synchronize);
    }

    [Fact]
    public void ParseMergeMode_KeepNewer()
    {
        (Database db, ProtectedStream ps) = SetupV4();
        KdbxXmlReader reader = CreateReader(db, ps);

        XElement group = MinimalGroup();
        group.Add(new XElement("MergeMode", "KeepNewer"));

        XDocument doc = MinimalKeePassDoc(MinimalMeta(), group);
        FeedXml(reader, doc);

        db.RootGroup!.MergeMode.ShouldBe(MergeMode.KeepNewer);
    }

    [Fact]
    public void ParseMergeMode_Default_For_Unknown()
    {
        (Database db, ProtectedStream ps) = SetupV4();
        KdbxXmlReader reader = CreateReader(db, ps);

        XElement group = MinimalGroup();
        group.Add(new XElement("MergeMode", "Bogus"));

        XDocument doc = MinimalKeePassDoc(MinimalMeta(), group);
        FeedXml(reader, doc);

        db.RootGroup!.MergeMode.ShouldBe(MergeMode.Default);
    }

    // ── ParseEntryStrings missing Value ─────────────────────────────────

    [Fact]
    public void ParseEntryStrings_Missing_Value_Element_Skips()
    {
        (Database db, ProtectedStream ps) = SetupV4();
        KdbxXmlReader reader = CreateReader(db, ps);

        XElement entry = new(
            "Entry",
            new XElement("UUID", Convert.ToBase64String(Guid.NewGuid().ToByteArray())),
            new XElement("String", new XElement("Key", "Title"), new XElement("Value", "MyTitle")),
            new XElement("String", new XElement("Key", "MissingValue"))
        // intentionally no Value element
        );
        XElement group = MinimalGroup();
        group.Add(entry);

        XDocument doc = MinimalKeePassDoc(MinimalMeta(), group);
        FeedXml(reader, doc);

        Entry result = db.RootGroup!.Entries[0];
        result.Title.ShouldBe("MyTitle");
        result.Attributes.Contains("MissingValue").ShouldBeFalse();
    }

    // ── ParseEntryBinaries missing Ref / out of range ───────────────────

    [Fact]
    public void ParseEntryBinaries_Missing_Ref_Skips()
    {
        (Database db, ProtectedStream ps) = SetupV4();
        KdbxXmlReader reader = CreateReader(db, ps);

        XElement entry = new(
            "Entry",
            new XElement("UUID", Convert.ToBase64String(Guid.NewGuid().ToByteArray())),
            new XElement("String", new XElement("Key", "Title"), new XElement("Value", "T")),
            new XElement(
                "Binary",
                new XElement("Key", "file.bin"),
                new XElement("Value", "no ref here")
            )
        );
        XElement group = MinimalGroup();
        group.Add(entry);

        XDocument doc = MinimalKeePassDoc(MinimalMeta(), group);
        FeedXml(reader, doc);

        db.RootGroup!.Entries[0].Attachments.Keys.ShouldBeEmpty();
    }

    [Fact]
    public void ParseEntryBinaries_OutOfRange_Ref_Skips()
    {
        (Database db, ProtectedStream ps) = SetupV4();
        KdbxXmlReader reader = CreateReader(db, ps);

        XElement entry = new(
            "Entry",
            new XElement("UUID", Convert.ToBase64String(Guid.NewGuid().ToByteArray())),
            new XElement("String", new XElement("Key", "Title"), new XElement("Value", "T")),
            new XElement(
                "Binary",
                new XElement("Key", "file.bin"),
                new XElement("Value", new XAttribute("Ref", "999"))
            )
        );
        XElement group = MinimalGroup();
        group.Add(entry);

        XDocument doc = MinimalKeePassDoc(MinimalMeta(), group);
        FeedXml(reader, doc);

        db.RootGroup!.Entries[0].Attachments.Keys.ShouldBeEmpty();
    }

    // ── CustomData empty → null ─────────────────────────────────────────

    [Fact]
    public void ParseCustomData_Empty_Returns_Null()
    {
        (Database db, ProtectedStream ps) = SetupV4();
        KdbxXmlReader reader = CreateReader(db, ps);

        XElement group = MinimalGroup();
        group.Add(new XElement("CustomData")); // empty, no items

        XDocument doc = MinimalKeePassDoc(MinimalMeta(), group);
        FeedXml(reader, doc);

        db.RootGroup!.CustomData.ShouldBeNull();
    }

    // ── V3 round-trip with attachments ──────────────────────────────────

    [Fact]
    public void V3_RoundTrip_With_Attachments()
    {
        Database writeDb = MakeV3Db("v3attach");
        byte[] data = [0x01, 0x02, 0x03];
        Entry entry = new() { Title = "WithFile" };
        entry.Attachments.Set("test.bin", data);
        writeDb.RootGroup!.AddEntry(entry);

        using MemoryStream ms = new();
        new KdbxWriter(writeDb).WriteTo(ms);

        ms.Position = 0;
        Database readDb = new() { Key = new CompositeKey("v3attach") };
        new KdbxReader(readDb).ReadFrom(ms);

        readDb.RootGroup!.Entries[0].Attachments.Get("test.bin")!.ShouldBe(data);
    }

    [Fact]
    public void V3_RoundTrip_With_DeletedObjects()
    {
        Database writeDb = MakeV3Db("v3del");
        DateTime t1 = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        writeDb.DeletedObjects.Add(new DeletedObject(Guid.NewGuid(), t1));

        using MemoryStream ms = new();
        new KdbxWriter(writeDb).WriteTo(ms);

        ms.Position = 0;
        Database readDb = new() { Key = new CompositeKey("v3del") };
        new KdbxReader(readDb).ReadFrom(ms);

        readDb.DeletedObjects.Count.ShouldBe(1);
        readDb.DeletedObjects[0].DeletionTime.ShouldBe(t1);
    }

    [Fact]
    public void V3_RoundTrip_With_TriState_And_MergeMode()
    {
        Database writeDb = MakeV3Db("v3tristate");
        Group child = new()
        {
            Name = "Child",
            EnableAutoType = TriState.Disable,
            EnableSearching = TriState.Inherit,
            MergeMode = MergeMode.Synchronize,
        };
        writeDb.RootGroup!.AddGroup(child);

        using MemoryStream ms = new();
        new KdbxWriter(writeDb).WriteTo(ms);

        ms.Position = 0;
        Database readDb = new() { Key = new CompositeKey("v3tristate") };
        new KdbxReader(readDb).ReadFrom(ms);

        Group readChild = readDb.RootGroup!.Groups[0];
        readChild.EnableAutoType.ShouldBe(TriState.Disable);
        readChild.EnableSearching.ShouldBe(TriState.Inherit);
        readChild.MergeMode.ShouldBe(MergeMode.Synchronize);
    }

    [Fact]
    public void V3_RoundTrip_With_CustomData()
    {
        Database writeDb = MakeV3Db("v3cd");
        writeDb.Metadata!.CustomData = new CustomData();
        writeDb.Metadata.CustomData.Set("BrowserKey", "v3value");

        using MemoryStream ms = new();
        new KdbxWriter(writeDb).WriteTo(ms);

        ms.Position = 0;
        Database readDb = new() { Key = new CompositeKey("v3cd") };
        new KdbxReader(readDb).ReadFrom(ms);

        readDb.Metadata!.CustomData!.GetValue("BrowserKey").ShouldBe("v3value");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static Database MakeV3Db(string password)
    {
        IKdf kdf = new AesKdf(RandomNumberGenerator.GetBytes(32), 100_000);
        Settings settings = new() { Format = KdbxFormat.Kdbx3, Kdf = kdf };

        Database db = new()
        {
            Settings = settings,
            Metadata = new Metadata(),
            RootGroup = new Group { Name = "Root" },
            Key = new CompositeKey(password),
        };
        db.RootGroup.SetDatabaseRecursive(db);
        return db;
    }
}
