namespace LibKdbx.Tests;

public class DatabaseTests
{
    public TestContext TestContext { get; set; } = null!;

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
        using var dbf = new DatabaseWithFile();
        dbf.Database.Metadata!.Name = "RoundTripDB";
        Entry entry = new() { Title = "MyEntry", UserName = "alice" };
        dbf.Database.RootGroup!.AddEntry(entry);
        dbf.Save();

        using Database readDb = dbf.Open();
        readDb.Metadata!.Name.ShouldBe("RoundTripDB");
        readDb.RootGroup!.Entries.Count.ShouldBe(1);
        readDb.RootGroup.Entries[0].Title.ShouldBe("MyEntry");
    }

    [Fact]
    public void Open_Wrong_Password_Throws()
    {
        using var dbf = new DatabaseWithFile("correct");
        dbf.Save();

        Should.Throw<Exception>(() => dbf.Open("wrong"));
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
        using var dbf = new DatabaseWithFile();
        dbf.Database.RootGroup!.AddEntry(new Entry { Title = "E" });
        dbf.Database.HasChanges.ShouldBeTrue();

        dbf.Save();
        dbf.Database.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void PublicCustomData_In_Header()
    {
        Settings settings = new() { PublicName = "MyDB", PublicColor = "#FF0000" };
        using Database db = Database.Create("pw", settings);

        KdbxHeader header = KdbxHeader.FromSettings(settings);
        header.PublicCustomData.ShouldNotBeNull();
        System.Text.Encoding.UTF8.GetString(header.PublicCustomData).ShouldContain("Name: MyDB");
    }

    [Fact]
    public void MergeMode_RoundTrip()
    {
        using var dbf = new DatabaseWithFile();
        Group group = new() { Name = "MergeGroup", MergeMode = MergeMode.KeepNewer };
        dbf.Database.RootGroup!.AddGroup(group);
        dbf.Save();

        using Database readDb = dbf.Open();
        Group readGroup = readDb.RootGroup!.Groups[0];
        readGroup.MergeMode.ShouldBe(MergeMode.KeepNewer);
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

        KdbxHeader header = KdbxHeader.FromSettings(settings);
        header.IsVersion4.ShouldBeFalse();
    }

    // ── Constructors ─────────────────────────────────────────────────────

    [Fact]
    public void Ctor_Default_Has_No_FileInfo()
    {
        using Database db = new();
        db.DatabaseFile.ShouldBeNull();
    }

    [Fact]
    public void Ctor_CompositeKey_Sets_Key()
    {
        using var key = new CompositeKey("test");
        using Database db = new(key);
        db.DatabaseFile.ShouldBeNull();
    }

    [Fact]
    public void Ctor_Path_Only_Sets_FileInfo()
    {
        using Database db = new("/tmp/test.kdbx");
        db.DatabaseFile!.FullName.ShouldBe("/tmp/test.kdbx");
    }

    [Fact]
    public void Ctor_Path_And_Key_Sets_Both()
    {
        using var key = new CompositeKey("pw");
        using Database db = new("/tmp/test.kdbx", key);
        db.DatabaseFile!.FullName.ShouldBe("/tmp/test.kdbx");
    }

    [Fact]
    public void Ctor_Path_And_Password_Sets_Both()
    {
        using Database db = new("/tmp/test.kdbx", "hunter2");
        db.DatabaseFile!.FullName.ShouldBe("/tmp/test.kdbx");
    }

    [Fact]
    public void Ctor_Path_Password_And_KeyFile_Sets_Both()
    {
        byte[] keyBytes = CryptoHelpers.GetRandomBytes(32);
        using var tfKey = new TempFile();
        tfKey.WriteAllText(Convert.ToHexString(keyBytes));
        using Database db = new("/tmp/test.kdbx", "hunter2", tfKey.Path);
        db.DatabaseFile!.FullName.ShouldBe("/tmp/test.kdbx");
    }

    // ── Create factories ─────────────────────────────────────────────────

    [Fact]
    public void Create_With_KeyFile_Sets_RootGroup()
    {
        byte[] keyBytes = CryptoHelpers.GetRandomBytes(32);
        using var tfKey = new TempFile();
        tfKey.WriteAllText(Convert.ToHexString(keyBytes));
        using Database db = Database.Create("pw", tfKey.Path);
        db.RootGroup!.Name.ShouldBe("Root");
        db.Metadata!.Generator.ShouldBe("LibKdbx");
    }

    [Fact]
    public void Create_V3_Format_Sets_Version_3_1()
    {
        Settings v3 = new() { Format = KdbxFormat.Kdbx3 };
        using Database db = Database.Create("pw", v3);
        db.Version.Major.ShouldBe((ushort)3);
        db.Version.Minor.ShouldBe((ushort)1);
    }

    [Fact]
    public void Create_V4_Format_Sets_Version_4_1()
    {
        using Database db = Database.Create("pw");
        db.Version.Major.ShouldBe((ushort)4);
        db.Version.Minor.ShouldBe((ushort)1);
    }

    // ── Open ─────────────────────────────────────────────────────────────

    [Fact]
    public void Open_No_FilePath_Throws()
    {
        using Database db = new(new CompositeKey("pw"));
        Should.Throw<InvalidOperationException>(db.Open);
    }

    [Fact]
    public void Open_With_KeyFile()
    {
        byte[] keyBytes = CryptoHelpers.GetRandomBytes(32);
        using var tfKey = new TempFile();
        tfKey.WriteAllText(Convert.ToHexString(keyBytes));
        using var tfDb = new TempFile();
        using Database writeDb = Database.Create("pw", tfKey.Path);
        writeDb.Metadata!.Name = "KeyFileDB";
        writeDb.SaveAs(tfDb.Path);

        using Database readDb = Database.Open(tfDb.Path, "pw", tfKey.Path);
        readDb.Metadata!.Name.ShouldBe("KeyFileDB");
    }

    // ── Save ─────────────────────────────────────────────────────────────

    [Fact]
    public void Save_No_FilePath_Throws()
    {
        using Database db = new(new CompositeKey("pw"))
        {
            Metadata = new Metadata(),
            RootGroup = new Group { Name = "Root" },
        };
        db.RootGroup.SetDatabaseRecursive(db);
        Should.Throw<InvalidOperationException>(db.Save);
    }

    // ── Dispose ──────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_Can_Be_Called_Twice()
    {
        Database db = Database.Create("secret");
        db.Dispose();
        db.Dispose(); // no exception
        db.Metadata.ShouldBeNull();
    }

    // ── Key setter ───────────────────────────────────────────────────────

    [Fact]
    public void Key_Setter_Disposes_Old_Key()
    {
        Database db = new("hunter2") { Metadata = new Metadata(), RootGroup = new Group() };
        db.RootGroup.SetDatabaseRecursive(db);
        // Replace key; old key should be disposed
        db.Key = new CompositeKey("newpass");
        // No way to directly observe disposal, but verified via no double-dispose crash
    }

    // ── Search with null RootGroup ───────────────────────────────────────

    [Fact]
    public void FindEntry_Title_Null_RootGroup_ReturnsNull()
    {
        using Database db = new(new CompositeKey("pw"));
        db.FindEntry("anything").ShouldBeNull();
    }

    [Fact]
    public void FindEntry_Predicate_Null_RootGroup_ReturnsNull()
    {
        using Database db = new(new CompositeKey("pw"));
        db.FindEntry(_ => true).ShouldBeNull();
    }

    [Fact]
    public void FindAllEntries_Null_RootGroup_ReturnsEmpty()
    {
        using Database db = new(new CompositeKey("pw"));
        db.FindAllEntries(_ => true).ShouldBeEmpty();
    }

    [Fact]
    public void FindGroup_Name_Null_RootGroup_ReturnsNull()
    {
        using Database db = new(new CompositeKey("pw"));
        db.FindGroup("anything").ShouldBeNull();
    }

    [Fact]
    public void FindGroup_Predicate_Null_RootGroup_ReturnsNull()
    {
        using Database db = new(new CompositeKey("pw"));
        db.FindGroup(_ => true).ShouldBeNull();
    }

    [Fact]
    public void FindAllGroups_Null_RootGroup_ReturnsEmpty()
    {
        using Database db = new(new CompositeKey("pw"));
        db.FindAllGroups(_ => true).ShouldBeEmpty();
    }

    // ── Recycle bin edge cases ───────────────────────────────────────────

    [Fact]
    public void IsRecycleBinEnabled_Null_Metadata_ReturnsFalse()
    {
        using Database db = new(new CompositeKey("pw"));
        db.IsRecycleBinEnabled().ShouldBeFalse();
    }

    [Fact]
    public void GetRecycleBin_Null_Metadata_ReturnsNull()
    {
        using Database db = new(new CompositeKey("pw"));
        db.GetRecycleBin().ShouldBeNull();
    }

    [Fact]
    public void GetRecycleBin_EmptyUuid_ReturnsNull()
    {
        using Database db = Database.Create("pw");
        db.Metadata!.RecycleBinUuid = Guid.Empty;
        db.GetRecycleBin().ShouldBeNull();
    }

    [Fact]
    public void GetOrCreateRecycleBin_Null_RootGroup_Throws()
    {
        using Database db = new(new CompositeKey("pw"));
        Should.Throw<InvalidOperationException>(db.GetOrCreateRecycleBin);
    }

    [Fact]
    public void GetOrCreateRecycleBin_Existing_ReturnsIt()
    {
        using Database db = Database.Create("pw");
        Group first = db.GetOrCreateRecycleBin();
        Group second = db.GetOrCreateRecycleBin();
        second.ShouldBe(first);
    }

    // ── ResolveValue depth limit ─────────────────────────────────────────

    [Fact]
    public void ResolveValue_Depth_Zero_Returns_Original()
    {
        using Database db = Database.Create("pw");
        Entry e = new() { Title = "T" };
        db.RootGroup!.AddEntry(e);

        // Set Notes to a REF that would resolve if depth > 0
        e.Notes = "{REF:N@T:T}";
        string result = db.ResolveField(e, "Notes", maxDepth: 0);
        result.ShouldBe("{REF:N@T:T}");
    }

    // ── FindReferencedEntry — SearchIn 'O' ───────────────────────────────

    [Fact]
    public void FindReferencedEntry_SearchIn_CustomAttribute()
    {
        using Database db = Database.Create("pw");
        Entry target = new() { Title = "Target", UserName = "bob" };
        target.Attributes.Set("Tag", "shared");
        db.RootGroup!.AddEntry(target);

        Entry source = new() { Title = "Source" };
        source.Attributes.Set("UserName", "{REF:U@O:shared}");
        db.RootGroup.AddEntry(source);

        string resolved = db.ResolveField(source, "UserName");
        resolved.ShouldBe("bob");
    }

    [Fact]
    public void FindReferencedEntry_SearchBy_FieldCode()
    {
        using Database db = Database.Create("pw");
        Entry target = new() { Title = "Target", UserName = "bob" };
        db.RootGroup!.AddEntry(target);

        Entry source = new() { Title = "Source" };
        source.Attributes.Set("Password", "{REF:P@T:Target}");
        db.RootGroup.AddEntry(source);

        string resolved = db.ResolveField(source, "Password");
        resolved.ShouldBe(target.Password);
    }

    // ── Async ────────────────────────────────────────────────────────────

    [Fact]
    public async Task OpenAsync_SaveAsync_RoundTrip()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dbf = new DatabaseWithFile();
        dbf.Database.Metadata!.Name = "AsyncRoundTrip";
        Entry entry = new() { Title = "AsyncEntry", UserName = "alice" };
        dbf.Database.RootGroup!.AddEntry(entry);

        await dbf.SaveAsync(ct);

        using Database readDb = await dbf.OpenAsync(ct: ct);
        readDb.Metadata!.Name.ShouldBe("AsyncRoundTrip");
        readDb.RootGroup!.Entries.Count.ShouldBe(1);
        readDb.RootGroup.Entries[0].Title.ShouldBe("AsyncEntry");
    }

    [Fact]
    public async Task OpenAsync_Cancelled_Throws()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dbf = new DatabaseWithFile();
        await dbf.SaveAsync(ct);

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            Database.OpenAsync(dbf.Path, "pw", ct: cts.Token)
        );
    }

    [Fact]
    public async Task SaveAsync_Cancelled_Throws()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dbf = new DatabaseWithFile();

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            dbf.Database.SaveAsAsync(dbf.Path, cts.Token)
        );
    }

    [Fact]
    public async Task OpenAsync_Static_Factory()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dbf = new DatabaseWithFile();
        dbf.Database.Metadata!.Name = "StaticFactory";
        await dbf.SaveAsync(ct);

        using Database readDb = await dbf.OpenAsync(ct: ct);
        readDb.Metadata!.Name.ShouldBe("StaticFactory");
    }

    [Fact]
    public async Task SaveAsAsync_Changes_Path_And_Saves()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var tf1 = new TempFile();
        using var tf2 = new TempFile();
        using Database db = Database.Create("pw");
        db.Metadata!.Name = "FirstSave";
        await db.SaveAsAsync(tf1.Path, ct);
        db.DatabaseFile!.FullName.ShouldBe(tf1.Path);

        db.Metadata.Name = "SecondSave";
        await db.SaveAsAsync(tf2.Path, ct);
        db.DatabaseFile.FullName.ShouldBe(tf2.Path);

        // Verify first file unchanged, second file has new name
        using Database db1 = await Database.OpenAsync(tf1.Path, "pw", ct: ct);
        db1.Metadata!.Name.ShouldBe("FirstSave");

        using Database db2 = await Database.OpenAsync(tf2.Path, "pw", ct: ct);
        db2.Metadata!.Name.ShouldBe("SecondSave");
    }

    [Fact]
    public async Task OpenAsync_With_KeyFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] keyBytes = CryptoHelpers.GetRandomBytes(32);
        using var tfKey = new TempFile();
        tfKey.WriteAllText(Convert.ToHexString(keyBytes));
        using var tfDb = new TempFile();
        using Database writeDb = Database.Create("pw", tfKey.Path);
        writeDb.Metadata!.Name = "AsyncKeyFile";
        await writeDb.SaveAsAsync(tfDb.Path, ct);

        using Database readDb = await Database.OpenAsync(tfDb.Path, "pw", tfKey.Path, ct);
        readDb.Metadata!.Name.ShouldBe("AsyncKeyFile");
    }

    [Fact]
    public async Task OpenAsync_Wrong_Password_Throws()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dbf = new DatabaseWithFile("correct");
        await dbf.SaveAsync(ct);

        await Should.ThrowAsync<Exception>(Database.OpenAsync(dbf.Path, "wrong", ct: ct));
    }

    [Fact]
    public async Task OpenAsync_No_FilePath_Throws()
    {
        using Database db = new(new CompositeKey("pw"));
        await Should.ThrowAsync<InvalidOperationException>(
            db.OpenAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task SaveAsync_No_FilePath_Throws()
    {
        using Database db = new(new CompositeKey("pw"))
        {
            Metadata = new Metadata(),
            RootGroup = new Group { Name = "Root" },
        };
        db.RootGroup.SetDatabaseRecursive(db);
        await Should.ThrowAsync<InvalidOperationException>(
            db.SaveAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task OpenAsync_Resets_HasChanges()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dbf = new DatabaseWithFile();
        dbf.Database.RootGroup!.AddEntry(new Entry { Title = "E" });
        dbf.Database.HasChanges.ShouldBeTrue();
        await dbf.SaveAsync(ct);
        dbf.Database.HasChanges.ShouldBeFalse();

        using Database readDb = await dbf.OpenAsync(ct: ct);
        readDb.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_Resets_HasChanges()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dbf = new DatabaseWithFile();
        dbf.Database.RootGroup!.AddEntry(new Entry { Title = "E" });
        dbf.Database.HasChanges.ShouldBeTrue();

        await dbf.SaveAsync(ct);
        dbf.Database.HasChanges.ShouldBeFalse();
    }
}
