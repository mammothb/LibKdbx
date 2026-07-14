using System.Security.Cryptography;

namespace LibKdbx.Tests;

public class RoundTripTests
{
    [Fact]
    public void RoundTrip_Kdbx4_Minimal()
    {
        Database writeDb = MakeDb("hunter2", KdbxFormat.Kdbx4);
        writeDb.Metadata!.Name = "TestV4";

        using MemoryStream ms = new();
        new KdbxWriter(writeDb).WriteTo(ms);

        ms.Position = 0;
        Database readDb = new() { Key = new CompositeKey("hunter2") };
        new KdbxReader(readDb).ReadFrom(ms);

        readDb.Metadata!.Name.ShouldBe("TestV4");
        readDb.Metadata.Generator.ShouldBe("LibKdbx");
        readDb.RootGroup!.Name.ShouldBe("Root");
    }

    [Fact]
    public void RoundTrip_Kdbx3_Minimal()
    {
        Database writeDb = MakeDb("pass123", KdbxFormat.Kdbx3);
        writeDb.Metadata!.Name = "TestV3";

        using MemoryStream ms = new();
        new KdbxWriter(writeDb).WriteTo(ms);

        ms.Position = 0;
        Database readDb = new() { Key = new CompositeKey("pass123") };
        new KdbxReader(readDb).ReadFrom(ms);

        readDb.Metadata!.Name.ShouldBe("TestV3");
        readDb.RootGroup!.Name.ShouldBe("Root");
    }

    [Fact]
    public void RoundTrip_Kdbx4_With_Entry()
    {
        Database writeDb = MakeDb("secret", KdbxFormat.Kdbx4);
        Entry entry = new()
        {
            Title = "GitHub",
            UserName = "alice",
            Password = "s3cr3t!",
            Url = "https://github.com",
            Notes = "Work account",
        };
        writeDb.RootGroup!.AddEntry(entry);

        using MemoryStream ms = new();
        new KdbxWriter(writeDb).WriteTo(ms);

        ms.Position = 0;
        Database readDb = new() { Key = new CompositeKey("secret") };
        new KdbxReader(readDb).ReadFrom(ms);

        Entry readEntry = readDb.RootGroup!.Entries[0];
        readEntry.Title.ShouldBe("GitHub");
        readEntry.UserName.ShouldBe("alice");
        readEntry.Password.ShouldBe("s3cr3t!");
        readEntry.Url.ShouldBe("https://github.com");
        readEntry.Notes.ShouldBe("Work account");
    }

    [Fact]
    public void RoundTrip_Kdbx4_Protected_Field()
    {
        Database writeDb = MakeDb("pass", KdbxFormat.Kdbx4);
        Entry entry = new();
        entry.Attributes.Set("Password", "MySecret", protect: true);
        writeDb.RootGroup!.AddEntry(entry);

        using MemoryStream ms = new();
        new KdbxWriter(writeDb).WriteTo(ms);

        ms.Position = 0;
        Database readDb = new() { Key = new CompositeKey("pass") };
        new KdbxReader(readDb).ReadFrom(ms);

        Entry readEntry = readDb.RootGroup!.Entries[0];
        readEntry.Password.ShouldBe("MySecret");
        readEntry.Attributes.IsProtected("Password").ShouldBeTrue();
    }

    [Fact]
    public void RoundTrip_Kdbx4_Attachments()
    {
        Database writeDb = MakeDb("key", KdbxFormat.Kdbx4);
        byte[] data = [0xDE, 0xAD, 0xBE, 0xEF];
        Entry entry = new() { Title = "Attached" };
        entry.Attachments.Set("note.txt", data);
        writeDb.RootGroup!.AddEntry(entry);

        using MemoryStream ms = new();
        new KdbxWriter(writeDb).WriteTo(ms);

        ms.Position = 0;
        Database readDb = new() { Key = new CompositeKey("key") };
        new KdbxReader(readDb).ReadFrom(ms);

        Entry readEntry = readDb.RootGroup!.Entries[0];
        readEntry.Attachments.Get("note.txt")!.ShouldBe(data);
    }

    [Fact]
    public void RoundTrip_Kdbx4_CustomData_On_Metadata()
    {
        Database writeDb = MakeDb("pw", KdbxFormat.Kdbx4);
        writeDb.Metadata!.CustomData = new CustomData();
        writeDb.Metadata.CustomData.Set("BrowserPrefix", "mybrowser_");

        using MemoryStream ms = new();
        new KdbxWriter(writeDb).WriteTo(ms);

        ms.Position = 0;
        Database readDb = new() { Key = new CompositeKey("pw") };
        new KdbxReader(readDb).ReadFrom(ms);

        readDb.Metadata!.CustomData!.GetValue("BrowserPrefix").ShouldBe("mybrowser_");
    }

    [Fact]
    public void RoundTrip_Kdbx4_CustomData_On_Entry()
    {
        Database writeDb = MakeDb("pw", KdbxFormat.Kdbx4);
        Entry entry = new() { Title = "CustomEntry", CustomData = new CustomData() };
        entry.CustomData.Set("PluginSetting", "enabled");
        writeDb.RootGroup!.AddEntry(entry);

        using MemoryStream ms = new();
        new KdbxWriter(writeDb).WriteTo(ms);

        ms.Position = 0;
        Database readDb = new() { Key = new CompositeKey("pw") };
        new KdbxReader(readDb).ReadFrom(ms);

        Entry readEntry = readDb.RootGroup!.Entries[0];
        readEntry.CustomData!.GetValue("PluginSetting").ShouldBe("enabled");
    }

    [Fact]
    public void RoundTrip_Kdbx4_TriState_Values()
    {
        Database writeDb = MakeDb("pw", KdbxFormat.Kdbx4);
        Group child = new()
        {
            Name = "Child",
            EnableAutoType = TriState.Disable,
            EnableSearching = TriState.Inherit,
        };
        writeDb.RootGroup!.AddGroup(child);

        using MemoryStream ms = new();
        new KdbxWriter(writeDb).WriteTo(ms);

        ms.Position = 0;
        Database readDb = new() { Key = new CompositeKey("pw") };
        new KdbxReader(readDb).ReadFrom(ms);

        Group readChild = readDb.RootGroup!.Groups[0];
        readChild.EnableAutoType.ShouldBe(TriState.Disable);
        readChild.EnableSearching.ShouldBe(TriState.Inherit);
    }

    [Fact]
    public void RoundTrip_Kdbx4_Group_NewFields()
    {
        Database writeDb = MakeDb("pw", KdbxFormat.Kdbx4);
        Group group = new()
        {
            Name = "NewGroup",
            Tags = "tag1;tag2",
            DefaultAutoTypeSequence = "{USERNAME}{TAB}{PASSWORD}",
            CustomData = new CustomData(),
        };
        group.CustomData.Set("share", "true");
        writeDb.RootGroup!.AddGroup(group);

        using MemoryStream ms = new();
        new KdbxWriter(writeDb).WriteTo(ms);

        ms.Position = 0;
        Database readDb = new() { Key = new CompositeKey("pw") };
        new KdbxReader(readDb).ReadFrom(ms);

        Group readGroup = readDb.RootGroup!.Groups[0];
        readGroup.Tags.ShouldBe("tag1;tag2");
        readGroup.DefaultAutoTypeSequence.ShouldBe("{USERNAME}{TAB}{PASSWORD}");
        readGroup.CustomData!.GetValue("share").ShouldBe("true");
    }

    [Fact]
    public void RoundTrip_Kdbx4_DeletedObjects()
    {
        Database writeDb = MakeDb("pw", KdbxFormat.Kdbx4);
        DateTime t1 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime t2 = new(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        writeDb._deletedObjects.Add(new DeletedObject(Guid.NewGuid(), t1));
        writeDb._deletedObjects.Add(new DeletedObject(Guid.NewGuid(), t2));

        using MemoryStream ms = new();
        new KdbxWriter(writeDb).WriteTo(ms);

        ms.Position = 0;
        Database readDb = new() { Key = new CompositeKey("pw") };
        new KdbxReader(readDb).ReadFrom(ms);

        readDb.DeletedObjects.Count.ShouldBe(2);
        readDb.DeletedObjects[0].DeletionTime.ShouldBe(t1);
        readDb.DeletedObjects[1].DeletionTime.ShouldBe(t2);
    }

    [Fact]
    public void RoundTrip_Kdbx4_ExcludeFromReports()
    {
        Database writeDb = MakeDb("pw", KdbxFormat.Kdbx4);
        Entry entry = new() { Title = "Excluded", ExcludeFromReports = true };
        writeDb.RootGroup!.AddEntry(entry);

        using MemoryStream ms = new();
        new KdbxWriter(writeDb).WriteTo(ms);

        ms.Position = 0;
        Database readDb = new() { Key = new CompositeKey("pw") };
        new KdbxReader(readDb).ReadFrom(ms);

        readDb.RootGroup!.Entries[0].ExcludeFromReports.ShouldBeTrue();
    }

    [Fact]
    public void RoundTrip_Kdbx4_EntryHistory()
    {
        Database writeDb = MakeDb("pw", KdbxFormat.Kdbx4);
        Entry entry = new() { Title = "V1" };
        writeDb.RootGroup!.AddEntry(entry);

        entry.Update(e => e.Title = "V2");

        using MemoryStream ms = new();
        new KdbxWriter(writeDb).WriteTo(ms);

        ms.Position = 0;
        Database readDb = new() { Key = new CompositeKey("pw") };
        new KdbxReader(readDb).ReadFrom(ms);

        Entry readEntry = readDb.RootGroup!.Entries[0];
        readEntry.Title.ShouldBe("V2");
        readEntry.History.Count.ShouldBe(1);
        readEntry.History[0].Title.ShouldBe("V1");
    }

    [Fact]
    public void Wrong_Password_Throws()
    {
        Database writeDb = MakeDb("correct", KdbxFormat.Kdbx4);

        using MemoryStream ms = new();
        new KdbxWriter(writeDb).WriteTo(ms);

        ms.Position = 0;
        Database readDb = new() { Key = new CompositeKey("wrong") };

        Should.Throw<InvalidDataException>(() => new KdbxReader(readDb).ReadFrom(ms));
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static Database MakeDb(string password, KdbxFormat format)
    {
        IKdf kdf =
            format == KdbxFormat.Kdbx3
                ? new AesKdf(RandomNumberGenerator.GetBytes(32), 100_000)
                : new Argon2Kdf(
                    RandomNumberGenerator.GetBytes(32),
                    parallelism: 2,
                    memoryKib: 16 * 1024,
                    iterations: 2
                );

        Settings settings = new() { Format = format, Kdf = kdf };

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
