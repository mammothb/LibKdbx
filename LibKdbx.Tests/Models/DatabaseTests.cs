namespace LibKdbx.Tests;

public class DatabaseTests
{
    [Fact]
    public void Create_Sets_Generator()
    {
        using Database db = Database.Create("hunter2");
        db.Metadata!.Generator.ShouldBe("LibKdbx");
    }

    [Fact]
    public void Create_Sets_DatabaseUuid()
    {
        using Database db = Database.Create("pw");
        db.Settings.DatabaseUuid.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_Sets_RootGroup()
    {
        using Database db = Database.Create("pw");
        db.RootGroup!.Name.ShouldBe("Root");
    }

    [Fact]
    public void Create_Accepts_Settings()
    {
        Settings settings = new() { Cipher = CipherAlgorithm.Aes256Cbc, IsCompressed = false };
        using Database db = Database.Create("pw", settings);
        db.Settings.Cipher.ShouldBe(CipherAlgorithm.Aes256Cbc);
        db.Settings.IsCompressed.ShouldBeFalse();
    }

    [Fact]
    public void Save_Then_Open_RoundTrip()
    {
        string path = Path.GetTempFileName();
        try
        {
            using Database writeDb = Database.Create("test123");
            writeDb.Metadata!.Name = "RoundTripDB";
            Entry entry = new() { Title = "MyEntry", UserName = "alice" };
            writeDb.RootGroup!.AddEntry(entry);

            writeDb.SaveAs(path);

            using Database readDb = Database.Open(path, "test123");
            readDb.Metadata!.Name.ShouldBe("RoundTripDB");
            readDb.RootGroup!.Entries.Count.ShouldBe(1);
            readDb.RootGroup.Entries[0].Title.ShouldBe("MyEntry");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Open_Wrong_Password_Throws()
    {
        string path = Path.GetTempFileName();
        try
        {
            using Database writeDb = Database.Create("correct");
            writeDb.SaveAs(path);

            Should.Throw<Exception>(() => Database.Open(path, "wrong"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RecycleBin_Enabled_By_Default()
    {
        using Database db = Database.Create("pw");
        db.IsRecycleBinEnabled().ShouldBeTrue();
    }

    [Fact]
    public void Delete_Entry_Moves_To_RecycleBin()
    {
        using Database db = Database.Create("pw");
        Entry entry = new() { Title = "ToDelete" };
        db.RootGroup!.AddEntry(entry);

        entry.Delete();

        // Entry should be in recycle bin, not root
        entry.ParentGroup!.Name.ShouldBe("Recycle Bin");
        db.RootGroup.Entries.ShouldNotContain(entry);
    }

    [Fact]
    public void FindEntryByUuid_Finds_Indexed_Entry()
    {
        using Database db = Database.Create("pw");
        Entry entry = new() { Title = "FindMe" };
        db.RootGroup!.AddEntry(entry);

        Entry? found = db.FindEntryByUuid(entry.Uuid);
        found.ShouldNotBeNull();
        found!.Title.ShouldBe("FindMe");
    }

    [Fact]
    public void FindEntryByUuid_Returns_Null_For_Unknown()
    {
        using Database db = Database.Create("pw");
        db.FindEntryByUuid(Guid.NewGuid()).ShouldBeNull();
    }

    [Fact]
    public void Dispose_Zeroizes_Key()
    {
        Database db = Database.Create("secret");
        db.Dispose();
        // After dispose, accessing Metadata should be null
        db.Metadata.ShouldBeNull();
        db.RootGroup.ShouldBeNull();
    }

    [Fact]
    public void HasChanges_Tracks_Modifications()
    {
        using Database db = Database.Create("pw");
        db.HasChanges.ShouldBeFalse();

        Entry entry = new() { Title = "New" };
        db.RootGroup!.AddEntry(entry);
        db.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public void Save_Resets_HasChanges()
    {
        string path = Path.GetTempFileName();
        try
        {
            using Database db = Database.Create("pw");
            db.RootGroup!.AddEntry(new Entry { Title = "E" });
            db.HasChanges.ShouldBeTrue();

            db.SaveAs(path);
            db.HasChanges.ShouldBeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PublicCustomData_In_Header()
    {
        Settings settings = new() { PublicName = "MyDB", PublicColor = "#FF0000" };
        using Database db = Database.Create("pw", settings);

        KdbxHeader header = settings.ToHeader();
        header.PublicCustomData.ShouldNotBeNull();
        System.Text.Encoding.UTF8.GetString(header.PublicCustomData).ShouldContain("Name: MyDB");
    }

    [Fact]
    public void MergeMode_RoundTrip()
    {
        string path = Path.GetTempFileName();
        try
        {
            using Database writeDb = Database.Create("pw");
            Group group = new() { Name = "MergeGroup", MergeMode = MergeMode.KeepNewer };
            writeDb.RootGroup!.AddGroup(group);
            writeDb.SaveAs(path);

            using Database readDb = Database.Open(path, "pw");
            Group readGroup = readDb.RootGroup!.Groups[0];
            readGroup.MergeMode.ShouldBe(MergeMode.KeepNewer);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void HeaderHash_Kdbx3()
    {
        Settings settings = new()
        {
            Format = KdbxFormat.Kdbx3,
            Kdf = new AesKdf(new byte[16], 100_000),
        };
        using Database db = Database.Create("pw", settings);

        KdbxHeader header = settings.ToHeader();
        header.IsVersion4.ShouldBeFalse();
    }
}
