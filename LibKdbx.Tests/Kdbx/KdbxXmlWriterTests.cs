using System.Security.Cryptography;
using System.Xml.Linq;

namespace LibKdbx.Tests;

public class KdbxXmlWriterTests
{
    private static (Database db, ProtectedStream ps, MemoryStream ms) Setup(
        bool isV4 = true,
        Settings? settings = null
    )
    {
        Database db = new()
        {
            Settings =
                settings ?? new Settings { Format = isV4 ? KdbxFormat.Kdbx4 : KdbxFormat.Kdbx3 },
            Metadata = new Metadata { Generator = "TestGen" },
            RootGroup = new Group { Name = "Root" },
            Key = new CompositeKey("test"),
        };
        byte[] psKey = RandomNumberGenerator.GetBytes(64);
        ProtectedStream ps = new(ProtectedStreamAlgorithm.ChaCha20, psKey);
        MemoryStream ms = new();
        return (db, ps, ms);
    }

    private static XDocument ParseXml(Stream stream)
    {
        stream.Position = 0;
        return XDocument.Load(stream);
    }

    // ── Metadata ──────────────────────────────────────────────────────────

    [Fact]
    public void Metadata_All_Five_MemoryProtection_Flags()
    {
        (Database db, ProtectedStream ps, MemoryStream ms) = Setup();
        db.Metadata!.ProtectTitle = true;
        db.Metadata.ProtectUserName = true;
        db.Metadata.ProtectPassword = false;
        db.Metadata.ProtectUrl = true;
        db.Metadata.ProtectNotes = false;

        new KdbxXmlWriter(db, ps, isV4: true).WriteTo(ms);
        XDocument doc = ParseXml(ms);

        XElement mp = doc.Root!.Element("Meta")!.Element("MemoryProtection")!;
        mp.Element("ProtectTitle")!.Value.ShouldBe("True");
        mp.Element("ProtectUserName")!.Value.ShouldBe("True");
        mp.Element("ProtectPassword")!.Value.ShouldBe("False");
        mp.Element("ProtectURL")!.Value.ShouldBe("True");
        mp.Element("ProtectNotes")!.Value.ShouldBe("False");
    }

    [Fact]
    public void Metadata_All_New_Fields()
    {
        (Database db, ProtectedStream ps, MemoryStream ms) = Setup();
        Metadata meta = db.Metadata!;
        meta.Generator = "MyApp";
        meta.NameChanged = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        meta.DescriptionChanged = new DateTime(2024, 2, 20, 0, 0, 0, DateTimeKind.Utc);
        meta.DefaultUserNameChanged = new DateTime(2024, 3, 10, 0, 0, 0, DateTimeKind.Utc);
        meta.MaintenanceHistoryDays = 90;
        meta.Color = "#00FF00";
        meta.RecycleBinChanged = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        meta.MasterKeyChangeRec = 30;
        meta.MasterKeyChangeForce = 365;
        meta.EntryTemplatesGroup = Guid.NewGuid();
        meta.EntryTemplatesGroupChanged = new DateTime(2024, 5, 5, 0, 0, 0, DateTimeKind.Utc);
        meta.LastSelectedGroup = Guid.NewGuid();
        meta.LastTopVisibleGroup = Guid.NewGuid();

        new KdbxXmlWriter(db, ps, isV4: true).WriteTo(ms);
        XDocument doc = ParseXml(ms);

        XElement el = doc.Root!.Element("Meta")!;
        el.Element("Generator")!.Value.ShouldBe("MyApp");
        el.Element("MaintenanceHistoryDays")!.Value.ShouldBe("90");
        el.Element("Color")!.Value.ShouldBe("#00FF00");
        el.Element("MasterKeyChangeRec")!.Value.ShouldBe("30");
        el.Element("MasterKeyChangeForce")!.Value.ShouldBe("365");
        el.Element("EntryTemplatesGroup")!.ShouldNotBeNull();
        el.Element("LastSelectedGroup")!.ShouldNotBeNull();
        el.Element("LastTopVisibleGroup")!.ShouldNotBeNull();
    }

    [Fact]
    public void Metadata_CustomData()
    {
        (Database db, ProtectedStream ps, MemoryStream ms) = Setup();
        db.Metadata!.CustomData = new CustomData();
        db.Metadata.CustomData.Set("BrowserKey", "somevalue");

        new KdbxXmlWriter(db, ps, isV4: true).WriteTo(ms);
        XDocument doc = ParseXml(ms);

        XElement cd = doc.Root!.Element("Meta")!.Element("CustomData")!;
        cd.Elements("Item").Count().ShouldBe(1);
        cd.Element("Item")!.Element("Key")!.Value.ShouldBe("BrowserKey");
        cd.Element("Item")!.Element("Value")!.Value.ShouldBe("somevalue");
    }

    // ── Group ─────────────────────────────────────────────────────────────

    [Fact]
    public void Group_TriState_EnableAutoType_Inherit()
    {
        (Database db, ProtectedStream ps, MemoryStream ms) = Setup();
        db.RootGroup!.EnableAutoType = TriState.Inherit;

        new KdbxXmlWriter(db, ps, isV4: true).WriteTo(ms);
        XDocument doc = ParseXml(ms);

        doc.Root!.Element("Root")!
            .Element("Group")!
            .Element("EnableAutoType")!
            .Value.ShouldBe("null");
    }

    [Fact]
    public void Group_TriState_EnableAutoType_Enable()
    {
        (Database db, ProtectedStream ps, MemoryStream ms) = Setup();
        db.RootGroup!.EnableAutoType = TriState.Enable;

        new KdbxXmlWriter(db, ps, isV4: true).WriteTo(ms);
        XDocument doc = ParseXml(ms);

        doc.Root!.Element("Root")!
            .Element("Group")!
            .Element("EnableAutoType")!
            .Value.ShouldBe("True");
    }

    [Fact]
    public void Group_TriState_EnableSearching_Disable()
    {
        (Database db, ProtectedStream ps, MemoryStream ms) = Setup();
        db.RootGroup!.EnableSearching = TriState.Disable;

        new KdbxXmlWriter(db, ps, isV4: true).WriteTo(ms);
        XDocument doc = ParseXml(ms);

        doc.Root!.Element("Root")!
            .Element("Group")!
            .Element("EnableSearching")!
            .Value.ShouldBe("False");
    }

    [Fact]
    public void Group_NewFields()
    {
        (Database db, ProtectedStream ps, MemoryStream ms) = Setup();
        Group group = db.RootGroup!;
        group.Tags = "a;b;c";
        group.DefaultAutoTypeSequence = "{TAB}{PASSWORD}";
        group.LastTopVisibleEntry = Guid.NewGuid();
        group.PreviousParentGroup = Guid.NewGuid();
        group.CustomData = new CustomData();
        group.CustomData.Set("x", "y");

        new KdbxXmlWriter(db, ps, isV4: true).WriteTo(ms);
        XDocument doc = ParseXml(ms);

        XElement gEl = doc.Root!.Element("Root")!.Element("Group")!;
        gEl.Element("Tags")!.Value.ShouldBe("a;b;c");
        gEl.Element("DefaultAutoTypeSequence")!.Value.ShouldBe("{TAB}{PASSWORD}");
        gEl.Element("LastTopVisibleEntry")!.ShouldNotBeNull();
        gEl.Element("PreviousParentGroup")!.ShouldNotBeNull();
        gEl.Element("CustomData")!.ShouldNotBeNull();
    }

    // ── Entry ─────────────────────────────────────────────────────────────

    [Fact]
    public void Entry_QualityCheck_Inverted()
    {
        (Database db, ProtectedStream ps, MemoryStream ms) = Setup();
        Entry entry = new() { ExcludeFromReports = true };
        db.RootGroup!.AddEntry(entry);

        new KdbxXmlWriter(db, ps, isV4: true).WriteTo(ms);
        XDocument doc = ParseXml(ms);

        doc.Root!.Element("Root")!
            .Element("Group")!
            .Element("Entry")!
            .Element("QualityCheck")!
            .Value.ShouldBe("False");
    }

    [Fact]
    public void Entry_QualityCheck_Normal()
    {
        (Database db, ProtectedStream ps, MemoryStream ms) = Setup();
        Entry entry = new() { ExcludeFromReports = false };
        db.RootGroup!.AddEntry(entry);

        new KdbxXmlWriter(db, ps, isV4: true).WriteTo(ms);
        XDocument doc = ParseXml(ms);

        doc.Root!.Element("Root")!
            .Element("Group")!
            .Element("Entry")!
            .Element("QualityCheck")!
            .Value.ShouldBe("True");
    }

    [Fact]
    public void Entry_NewFields()
    {
        (Database db, ProtectedStream ps, MemoryStream ms) = Setup();
        Entry entry = new()
        {
            Title = "TestEntry",
            PreviousParentGroup = Guid.NewGuid(),
            ExcludeFromReports = true,
            CustomData = new CustomData(),
        };
        entry.CustomData.Set("k", "v");
        db.RootGroup!.AddEntry(entry);

        new KdbxXmlWriter(db, ps, isV4: true).WriteTo(ms);
        XDocument doc = ParseXml(ms);

        XElement eEl = doc.Root!.Element("Root")!.Element("Group")!.Element("Entry")!;
        eEl.Element("PreviousParentGroup")!.ShouldNotBeNull();
        eEl.Element("QualityCheck")!.Value.ShouldBe("False");
        eEl.Element("CustomData")!.ShouldNotBeNull();
    }

    [Fact]
    public void EntryAttributes_Default_Before_Custom()
    {
        (Database db, ProtectedStream ps, MemoryStream ms) = Setup();
        Entry entry = new() { Title = "MyTitle" };
        entry.Attributes.Set("ZKey", "zval");
        entry.Attributes.Set("AKey", "aval");
        db.RootGroup!.AddEntry(entry);

        new KdbxXmlWriter(db, ps, isV4: true).WriteTo(ms);
        XDocument doc = ParseXml(ms);

        List<XElement> strings =
        [
            .. doc.Root!.Element("Root")!.Element("Group")!.Element("Entry")!.Elements("String"),
        ];

        strings[0].Element("Key")!.Value.ShouldBe("Title");
        strings[4].Element("Key")!.Value.ShouldBe("Notes");
        strings[5].Element("Key")!.Value.ShouldBe("ZKey");
        strings[6].Element("Key")!.Value.ShouldBe("AKey");
    }

    [Fact]
    public void EntryAttributes_Protected_Flag()
    {
        (Database db, ProtectedStream ps, MemoryStream ms) = Setup();
        Entry entry = new() { Title = "PublicTitle" };
        entry.Attributes.Set("Secret", "secretval", protect: true);
        db.RootGroup!.AddEntry(entry);

        new KdbxXmlWriter(db, ps, isV4: true).WriteTo(ms);
        XDocument doc = ParseXml(ms);

        List<XElement> strings =
        [
            .. doc.Root!.Element("Root")!.Element("Group")!.Element("Entry")!.Elements("String"),
        ];

        XElement secretEl = strings.First(s => s.Element("Key")!.Value == "Secret");
        secretEl.Element("Value")!.Attribute("Protected")!.Value.ShouldBe("True");
    }

    [Fact]
    public void BinaryPool_Deduplication()
    {
        (Database db, ProtectedStream ps, MemoryStream ms) = Setup();
        byte[] sameData = [1, 2, 3, 4, 5];

        Entry e1 = new() { Title = "Entry1" };
        e1.Attachments.Set("file.bin", sameData);
        Entry e2 = new() { Title = "Entry2" };
        e2.Attachments.Set("file.bin", sameData);

        db.RootGroup!.AddEntry(e1);
        db.RootGroup.AddEntry(e2);

        new KdbxXmlWriter(db, ps, isV4: true).WriteTo(ms);
        XDocument doc = ParseXml(ms);

        List<XElement> entries =
        [
            .. doc.Root!.Element("Root")!.Element("Group")!.Elements("Entry"),
        ];
        entries.Count.ShouldBe(2);

        foreach (XElement eEl in entries)
        {
            XAttribute? refAttr = eEl.Element("Binary")!.Element("Value")!.Attribute("Ref");
            refAttr!.Value.ShouldBe("0");
        }
    }

    // ── Deleted Objects ──────────────────────────────────────────────────

    [Fact]
    public void DeletedObjects()
    {
        (Database db, ProtectedStream ps, MemoryStream ms) = Setup();
        DateTime t1 = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime t2 = new(2024, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        db.DeletedObjects.Add(new DeletedObject(Guid.NewGuid(), t1));
        db.DeletedObjects.Add(new DeletedObject(Guid.NewGuid(), t2));

        new KdbxXmlWriter(db, ps, isV4: true).WriteTo(ms);
        XDocument doc = ParseXml(ms);

        XElement delEl = doc.Root!.Element("Root")!.Element("DeletedObjects")!;
        delEl.Elements("DeletedObject").Count().ShouldBe(2);
    }
}
