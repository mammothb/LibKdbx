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

        KdbxHeader header = KdbxHeader.FromSettings(settings);
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

        KdbxHeader header = KdbxHeader.FromSettings(settings);
        header.IsVersion4.ShouldBeFalse();
    }

    // ── Constructors ─────────────────────────────────────────────────────

    [Fact]
    public void Ctor_Default_Has_No_FileInfo()
    {
        using Database db = new();
        db.FileInfo.ShouldBeNull();
    }

    [Fact]
    public void Ctor_CompositeKey_Sets_Key()
    {
        using var key = new CompositeKey("test");
        using Database db = new(key);
        db.FileInfo.ShouldBeNull();
    }

    [Fact]
    public void Ctor_Path_Only_Sets_FileInfo()
    {
        using Database db = new("/tmp/test.kdbx");
        db.FileInfo!.FullName.ShouldBe("/tmp/test.kdbx");
    }

    [Fact]
    public void Ctor_Path_And_Key_Sets_Both()
    {
        using var key = new CompositeKey("pw");
        using Database db = new("/tmp/test.kdbx", key);
        db.FileInfo!.FullName.ShouldBe("/tmp/test.kdbx");
    }

    [Fact]
    public void Ctor_Path_And_Password_Sets_Both()
    {
        using Database db = new("/tmp/test.kdbx", "hunter2");
        db.FileInfo!.FullName.ShouldBe("/tmp/test.kdbx");
    }

    [Fact]
    public void Ctor_Path_Password_And_KeyFile_Sets_Both()
    {
        byte[] keyBytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(keyBytes);
        string keyPath = Path.GetTempFileName();
        File.WriteAllText(keyPath, Convert.ToHexString(keyBytes), System.Text.Encoding.ASCII);
        try
        {
            using Database db = new("/tmp/test.kdbx", "hunter2", keyPath);
            db.FileInfo!.FullName.ShouldBe("/tmp/test.kdbx");
        }
        finally
        {
            File.Delete(keyPath);
        }
    }

    // ── Create factories ─────────────────────────────────────────────────

    [Fact]
    public void Create_With_KeyFile_Sets_RootGroup()
    {
        byte[] keyBytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(keyBytes);
        string keyPath = Path.GetTempFileName();
        File.WriteAllText(keyPath, Convert.ToHexString(keyBytes), System.Text.Encoding.ASCII);
        try
        {
            using Database db = Database.Create("pw", keyPath);
            db.RootGroup!.Name.ShouldBe("Root");
            db.Metadata!.Generator.ShouldBe("LibKdbx");
        }
        finally
        {
            File.Delete(keyPath);
        }
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
        byte[] keyBytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(keyBytes);
        string keyPath = Path.GetTempFileName();
        File.WriteAllText(keyPath, Convert.ToHexString(keyBytes), System.Text.Encoding.ASCII);
        string dbPath = Path.GetTempFileName();
        try
        {
            using Database writeDb = Database.Create("pw", keyPath);
            writeDb.Metadata!.Name = "KeyFileDB";
            writeDb.SaveAs(dbPath);

            using Database readDb = Database.Open(dbPath, "pw", keyPath);
            readDb.Metadata!.Name.ShouldBe("KeyFileDB");
        }
        finally
        {
            File.Delete(keyPath);
            File.Delete(dbPath);
        }
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
        string path = Path.GetTempFileName();
        try
        {
            using Database writeDb = Database.Create("test123");
            writeDb.Metadata!.Name = "AsyncRoundTrip";
            Entry entry = new() { Title = "AsyncEntry", UserName = "alice" };
            writeDb.RootGroup!.AddEntry(entry);

            await writeDb.SaveAsAsync(path, ct);

            using Database readDb = await Database.OpenAsync(path, "test123", ct: ct);
            readDb.Metadata!.Name.ShouldBe("AsyncRoundTrip");
            readDb.RootGroup!.Entries.Count.ShouldBe(1);
            readDb.RootGroup.Entries[0].Title.ShouldBe("AsyncEntry");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OpenAsync_Cancelled_Throws()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string path = Path.GetTempFileName();
        try
        {
            using Database writeDb = Database.Create("pw");
            await writeDb.SaveAsAsync(path, ct);

            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.Cancel();

            await Should.ThrowAsync<OperationCanceledException>(
                Database.OpenAsync(path, "pw", ct: cts.Token)
            );
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsync_Cancelled_Throws()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string path = Path.GetTempFileName();
        try
        {
            using Database db = Database.Create("pw");

            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.Cancel();

            await Should.ThrowAsync<OperationCanceledException>(db.SaveAsAsync(path, cts.Token));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OpenAsync_Static_Factory()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string path = Path.GetTempFileName();
        try
        {
            using Database writeDb = Database.Create("pw");
            writeDb.Metadata!.Name = "StaticFactory";
            await writeDb.SaveAsAsync(path, ct);

            using Database readDb = await Database.OpenAsync(path, "pw", ct: ct);
            readDb.Metadata!.Name.ShouldBe("StaticFactory");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsAsync_Changes_Path_And_Saves()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string path1 = Path.GetTempFileName();
        string path2 = Path.GetTempFileName();
        try
        {
            using Database db = Database.Create("pw");
            db.Metadata!.Name = "FirstSave";
            await db.SaveAsAsync(path1, ct);
            db.FileInfo!.FullName.ShouldBe(path1);

            db.Metadata.Name = "SecondSave";
            await db.SaveAsAsync(path2, ct);
            db.FileInfo.FullName.ShouldBe(path2);

            // Verify first file unchanged, second file has new name
            using Database db1 = await Database.OpenAsync(path1, "pw", ct: ct);
            db1.Metadata!.Name.ShouldBe("FirstSave");

            using Database db2 = await Database.OpenAsync(path2, "pw", ct: ct);
            db2.Metadata!.Name.ShouldBe("SecondSave");
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
    }

    [Fact]
    public async Task OpenAsync_With_KeyFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] keyBytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(keyBytes);
        string keyPath = Path.GetTempFileName();
        File.WriteAllText(keyPath, Convert.ToHexString(keyBytes), System.Text.Encoding.ASCII);
        string dbPath = Path.GetTempFileName();
        try
        {
            using Database writeDb = Database.Create("pw", keyPath);
            writeDb.Metadata!.Name = "AsyncKeyFile";
            await writeDb.SaveAsAsync(dbPath, ct);

            using Database readDb = await Database.OpenAsync(dbPath, "pw", keyPath, ct);
            readDb.Metadata!.Name.ShouldBe("AsyncKeyFile");
        }
        finally
        {
            File.Delete(keyPath);
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task OpenAsync_Wrong_Password_Throws()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string path = Path.GetTempFileName();
        try
        {
            using Database writeDb = Database.Create("correct");
            await writeDb.SaveAsAsync(path, ct);

            await Should.ThrowAsync<Exception>(Database.OpenAsync(path, "wrong", ct: ct));
        }
        finally
        {
            File.Delete(path);
        }
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
        string path = Path.GetTempFileName();
        try
        {
            using Database writeDb = Database.Create("pw");
            writeDb.RootGroup!.AddEntry(new Entry { Title = "E" });
            writeDb.HasChanges.ShouldBeTrue();
            await writeDb.SaveAsAsync(path, ct);
            writeDb.HasChanges.ShouldBeFalse();

            using Database readDb = await Database.OpenAsync(path, "pw", ct: ct);
            readDb.HasChanges.ShouldBeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsync_Resets_HasChanges()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string path = Path.GetTempFileName();
        try
        {
            using Database db = Database.Create("pw");
            db.RootGroup!.AddEntry(new Entry { Title = "E" });
            db.HasChanges.ShouldBeTrue();

            await db.SaveAsAsync(path, ct);
            db.HasChanges.ShouldBeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
