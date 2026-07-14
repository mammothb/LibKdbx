namespace LibKdbx.Tests;

public class MergerTests
{
    private static (Database Source, Database Target) CreatePair()
    {
        Database source = Database.Create("source");
        Database target = Database.Create("target");
        return (source, target);
    }

    // ── Basic merge ───────────────────────────────────────────────────────

    [Fact]
    public void Merge_Empty_Source_Does_Nothing()
    {
        (Database source, Database target) = CreatePair();
        target.RootGroup!.Name = "TargetRoot";
        target.RootGroup.AddEntry(new Entry { Title = "Existing", Uuid = Guid.NewGuid() });

        Merger.Merge(source, target);

        target.RootGroup.Name.ShouldBe("TargetRoot");
        target.RootGroup.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public void Merge_New_Entry_Added()
    {
        (Database source, Database target) = CreatePair();
        Guid entryUuid = Guid.NewGuid();
        source.RootGroup!.AddEntry(new Entry { Title = "NewEntry", Uuid = entryUuid });

        Merger.Merge(source, target);

        target.RootGroup!.Entries.Count.ShouldBe(1);
        target.RootGroup.Entries[0].Title.ShouldBe("NewEntry");
        target.RootGroup.Entries[0].Uuid.ShouldBe(entryUuid);
    }

    [Fact]
    public void Merge_New_Group_Added()
    {
        (Database source, Database target) = CreatePair();
        Guid groupUuid = Guid.NewGuid();
        Group sub = new() { Name = "SubGroup", Uuid = groupUuid };
        sub.AddEntry(new Entry { Title = "SubEntry" });
        source.RootGroup!.AddGroup(sub);

        Merger.Merge(source, target);

        target.RootGroup!.Groups.Count.ShouldBe(1);
        Group merged = target.RootGroup.Groups[0];
        merged.Name.ShouldBe("SubGroup");
        merged.Uuid.ShouldBe(groupUuid);
        merged.Entries.Count.ShouldBe(1);
        merged.Entries[0].Title.ShouldBe("SubEntry");
    }

    // ── Conflict resolution — Default mode ────────────────────────────────

    [Fact]
    public void Merge_Default_Source_Newer_Wins()
    {
        (Database source, Database target) = CreatePair();
        Guid uuid = Guid.NewGuid();

        Entry sourceEntry = new() { Title = "SourceTitle", Uuid = uuid };
        sourceEntry.Times.LastModificationTime = new DateTime(2025, 1, 10);
        source.RootGroup!.AddEntry(sourceEntry);

        Entry targetEntry = new() { Title = "TargetTitle", Uuid = uuid };
        targetEntry.Times.LastModificationTime = new DateTime(2025, 1, 1);
        target.RootGroup!.AddEntry(targetEntry);

        Merger.Merge(source, target);

        target.RootGroup.Entries[0].Title.ShouldBe("SourceTitle");
    }

    [Fact]
    public void Merge_Default_Target_Newer_Keeps_Target()
    {
        (Database source, Database target) = CreatePair();
        Guid uuid = Guid.NewGuid();

        Entry sourceEntry = new() { Title = "SourceTitle", Uuid = uuid };
        sourceEntry.Times.LastModificationTime = new DateTime(2025, 1, 1);
        source.RootGroup!.AddEntry(sourceEntry);

        Entry targetEntry = new() { Title = "TargetTitle", Uuid = uuid };
        targetEntry.Times.LastModificationTime = new DateTime(2025, 1, 10);
        target.RootGroup!.AddEntry(targetEntry);

        Merger.Merge(source, target);

        target.RootGroup.Entries[0].Title.ShouldBe("TargetTitle");
    }

    [Fact]
    public void Merge_Synchronize_Source_Wins_Regardless_Of_Time()
    {
        (Database source, Database target) = CreatePair();
        Guid uuid = Guid.NewGuid();

        Entry sourceEntry = new() { Title = "SourceOld", Uuid = uuid };
        sourceEntry.Times.LastModificationTime = new DateTime(2024, 1, 1);
        source.RootGroup!.AddEntry(sourceEntry);

        Entry targetEntry = new() { Title = "TargetNew", Uuid = uuid };
        targetEntry.Times.LastModificationTime = new DateTime(2025, 6, 1);
        target.RootGroup!.AddEntry(targetEntry);

        Merger.Merge(source, target, MergeMode.Synchronize);

        target.RootGroup.Entries[0].Title.ShouldBe("SourceOld");
    }

    // ── Entry field merging ───────────────────────────────────────────────

    [Fact]
    public void Merge_Entry_All_Fields()
    {
        (Database source, Database target) = CreatePair();
        Guid uuid = Guid.NewGuid();

        Entry sourceEntry = new()
        {
            Title = "S_Title",
            UserName = "S_User",
            Password = "S_Pass",
            Url = "S_Url",
            Notes = "S_Notes",
            IconId = 42,
            ForegroundColor = "#FF0000",
            BackgroundColor = "#00FF00",
            OverrideUrl = "S_Override",
            Tags = "S_Tags",
            Uuid = uuid,
        };
        sourceEntry.Times.LastModificationTime = new DateTime(2025, 2, 1);
        sourceEntry.Attributes.Set("CustomKey", "CustomVal");
        source.RootGroup!.AddEntry(sourceEntry);

        Entry targetEntry = new()
        {
            Title = "T_Title",
            UserName = "T_User",
            Password = "T_Pass",
            Url = "T_Url",
            Notes = "T_Notes",
            Uuid = uuid,
        };
        targetEntry.Times.LastModificationTime = new DateTime(2025, 1, 1);
        target.RootGroup!.AddEntry(targetEntry);

        Merger.Merge(source, target);

        Entry result = target.RootGroup.Entries[0];
        result.Title.ShouldBe("S_Title");
        result.UserName.ShouldBe("S_User");
        result.Password.ShouldBe("S_Pass");
        result.Url.ShouldBe("S_Url");
        result.Notes.ShouldBe("S_Notes");
        result.IconId.ShouldBe(42);
        result.ForegroundColor.ShouldBe("#FF0000");
        result.BackgroundColor.ShouldBe("#00FF00");
        result.OverrideUrl.ShouldBe("S_Override");
        result.Tags.ShouldBe("S_Tags");
        result.Attributes.Get("CustomKey").ShouldBe("CustomVal");
    }

    [Fact]
    public void Merge_Entry_CustomAttributes_Preserved()
    {
        (Database source, Database target) = CreatePair();
        Guid uuid = Guid.NewGuid();

        Entry sourceEntry = new() { Title = "S", Uuid = uuid };
        sourceEntry.Times.LastModificationTime = new DateTime(2025, 2, 1);
        sourceEntry.Attributes.Set("A", "1");
        sourceEntry.Attributes.Set("B", "2");
        source.RootGroup!.AddEntry(sourceEntry);

        Entry targetEntry = new() { Title = "T", Uuid = uuid };
        targetEntry.Times.LastModificationTime = new DateTime(2025, 1, 1);
        targetEntry.Attributes.Set("B", "old");
        targetEntry.Attributes.Set("C", "3");
        target.RootGroup!.AddEntry(targetEntry);

        Merger.Merge(source, target);

        Entry result = target.RootGroup.Entries[0];
        result.Attributes.Get("A").ShouldBe("1"); // added from source
        result.Attributes.Get("B").ShouldBe("2"); // overwritten by source
        result.Attributes.Get("C").ShouldBe("3"); // preserved from target
    }

    [Fact]
    public void Merge_Synchronize_Removes_Target_Only_Attributes()
    {
        (Database source, Database target) = CreatePair();
        Guid uuid = Guid.NewGuid();

        Entry sourceEntry = new() { Title = "S", Uuid = uuid };
        sourceEntry.Times.LastModificationTime = new DateTime(2025, 2, 1);
        sourceEntry.Attributes.Set("Keep", "yes");
        source.RootGroup!.AddEntry(sourceEntry);

        Entry targetEntry = new() { Title = "T", Uuid = uuid };
        targetEntry.Times.LastModificationTime = new DateTime(2025, 1, 1);
        targetEntry.Attributes.Set("Keep", "old");
        targetEntry.Attributes.Set("Drop", "gone");
        target.RootGroup!.AddEntry(targetEntry);

        Merger.Merge(source, target, MergeMode.Synchronize);

        Entry result = target.RootGroup.Entries[0];
        result.Attributes.Get("Keep").ShouldBe("yes");
        result.Attributes.Contains("Drop").ShouldBeFalse();
    }

    [Fact]
    public void Merge_Synchronize_Removes_Target_Only_Attachments()
    {
        (Database source, Database target) = CreatePair();
        Guid uuid = Guid.NewGuid();

        Entry sourceEntry = new() { Title = "S", Uuid = uuid };
        sourceEntry.Times.LastModificationTime = new DateTime(2025, 2, 1);
        sourceEntry.Attachments.Set("keep.txt", "keep-data"u8.ToArray());
        source.RootGroup!.AddEntry(sourceEntry);

        Entry targetEntry = new() { Title = "T", Uuid = uuid };
        targetEntry.Times.LastModificationTime = new DateTime(2025, 1, 1);
        targetEntry.Attachments.Set("keep.txt", "old-data"u8.ToArray());
        targetEntry.Attachments.Set("drop.txt", "bye"u8.ToArray());
        target.RootGroup!.AddEntry(targetEntry);

        Merger.Merge(source, target, MergeMode.Synchronize);

        Entry result = target.RootGroup.Entries[0];
        result.Attachments.Contains("keep.txt").ShouldBeTrue();
        result.Attachments.Contains("drop.txt").ShouldBeFalse();
    }

    [Fact]
    public void Merge_Entry_AutoType_Merged()
    {
        (Database source, Database target) = CreatePair();
        Guid uuid = Guid.NewGuid();

        Entry sourceEntry = new() { Title = "S", Uuid = uuid };
        sourceEntry.Times.LastModificationTime = new DateTime(2025, 2, 1);
        sourceEntry.AutoType.Enabled = false;
        sourceEntry.AutoType.DefaultSequence = "{USERNAME}{TAB}{PASSWORD}{ENTER}";
        sourceEntry.AutoType.Associations.Add(
            new AutoTypeAssociation { Window = "Firefox", Sequence = "{PASSWORD}" }
        );
        source.RootGroup!.AddEntry(sourceEntry);

        Entry targetEntry = new() { Title = "T", Uuid = uuid };
        targetEntry.Times.LastModificationTime = new DateTime(2025, 1, 1);
        target.RootGroup!.AddEntry(targetEntry);

        Merger.Merge(source, target);

        Entry result = target.RootGroup.Entries[0];
        result.AutoType.Enabled.ShouldBeFalse();
        result.AutoType.DefaultSequence.ShouldBe("{USERNAME}{TAB}{PASSWORD}{ENTER}");
        result.AutoType.Associations.Count.ShouldBe(1);
        result.AutoType.Associations[0].Window.ShouldBe("Firefox");
    }

    // ── Group conflict ────────────────────────────────────────────────────

    [Fact]
    public void Merge_Group_Conflict_Source_Newer_Wins()
    {
        (Database source, Database target) = CreatePair();
        Guid uuid = Guid.NewGuid();

        Group sourceGroup = new()
        {
            Name = "SourceName",
            Notes = "SN",
            Uuid = uuid,
        };
        sourceGroup.Times.LastModificationTime = new DateTime(2025, 2, 1);
        source.RootGroup!.AddGroup(sourceGroup);

        Group targetGroup = new()
        {
            Name = "TargetName",
            Notes = "TN",
            Uuid = uuid,
        };
        targetGroup.Times.LastModificationTime = new DateTime(2025, 1, 1);
        target.RootGroup!.AddGroup(targetGroup);

        Merger.Merge(source, target);

        Group result = target.RootGroup.Groups[0];
        result.Name.ShouldBe("SourceName");
        result.Notes.ShouldBe("SN");
    }

    [Fact]
    public void Merge_Group_Conflict_Target_Newer_Keeps_Target()
    {
        (Database source, Database target) = CreatePair();
        Guid uuid = Guid.NewGuid();

        Group sourceGroup = new() { Name = "SourceName", Uuid = uuid };
        sourceGroup.Times.LastModificationTime = new DateTime(2025, 1, 1);
        source.RootGroup!.AddGroup(sourceGroup);

        Group targetGroup = new() { Name = "TargetName", Uuid = uuid };
        targetGroup.Times.LastModificationTime = new DateTime(2025, 2, 1);
        target.RootGroup!.AddGroup(targetGroup);

        Merger.Merge(source, target);

        target.RootGroup.Groups[0].Name.ShouldBe("TargetName");
    }

    // ── Nested groups ─────────────────────────────────────────────────────

    [Fact]
    public void Merge_Subgroups_Recursive()
    {
        (Database source, Database target) = CreatePair();
        Guid workUuid = Guid.NewGuid();
        Guid devUuid = Guid.NewGuid();

        Group sourceWork = new() { Name = "Work", Uuid = workUuid };
        Group sourceDev = new() { Name = "Dev", Uuid = devUuid };
        sourceDev.AddEntry(new Entry { Title = "DevEntry" });
        sourceWork.AddGroup(sourceDev);
        source.RootGroup!.AddGroup(sourceWork);

        Merger.Merge(source, target);

        target.RootGroup!.Groups.Count.ShouldBe(1);
        Group work = target.RootGroup.Groups[0];
        work.Groups.Count.ShouldBe(1);
        Group dev = work.Groups[0];
        dev.Entries.Count.ShouldBe(1);
        dev.Entries[0].Title.ShouldBe("DevEntry");
    }

    // ── Deletions ─────────────────────────────────────────────────────────

    [Fact]
    public void Merge_Deletions_Only_In_Synchronize_Mode()
    {
        (Database source, Database target) = CreatePair();
        Guid uuid = Guid.NewGuid();

        target.RootGroup!.AddEntry(new Entry { Title = "ToDelete", Uuid = uuid });
        source._deletedObjects.Add(
            new DeletedObject { Uuid = uuid, DeletionTime = DateTime.UtcNow }
        );

        // Default mode: deletion NOT applied
        Merger.Merge(source, target, MergeMode.Default);
        target.RootGroup.Entries.Count.ShouldBe(1);

        // Synchronize mode: deletion IS applied
        Merger.Merge(source, target, MergeMode.Synchronize);
        target.RootGroup.Entries.Count.ShouldBe(0);
    }

    [Fact]
    public void Merge_Deletions_Union()
    {
        (Database source, Database target) = CreatePair();

        var objA = new DeletedObject { Uuid = Guid.NewGuid(), DeletionTime = DateTime.UtcNow };
        var objB = new DeletedObject { Uuid = Guid.NewGuid(), DeletionTime = DateTime.UtcNow };

        source._deletedObjects.Add(objA);
        target._deletedObjects.Add(objB);

        Merger.Merge(source, target, MergeMode.Synchronize);

        target.DeletedObjects.Count.ShouldBe(2);
        target.DeletedObjects.Select(d => d.Uuid).ShouldContain(objA.Uuid);
        target.DeletedObjects.Select(d => d.Uuid).ShouldContain(objB.Uuid);
    }

    // ── History ───────────────────────────────────────────────────────────

    [Fact]
    public void Merge_History_Merged()
    {
        (Database source, Database target) = CreatePair();
        Guid uuid = Guid.NewGuid();

        Entry sourceEntry = new() { Title = "S_v2", Uuid = uuid };
        sourceEntry.Times.LastModificationTime = new DateTime(2025, 3, 1);
        sourceEntry._history.Add(new Entry { Title = "S_v1" });
        source.RootGroup!.AddEntry(sourceEntry);

        Entry targetEntry = new() { Title = "T_v1", Uuid = uuid };
        targetEntry.Times.LastModificationTime = new DateTime(2025, 2, 1);
        target.RootGroup!.AddEntry(targetEntry);

        Merger.Merge(source, target);

        // Source is newer → target becomes history, source becomes current
        Entry result = target.RootGroup.Entries[0];
        result.Title.ShouldBe("S_v2");
        result.History.Count.ShouldBe(2);
        result.History.Any(h => h.Title == "S_v1").ShouldBeTrue();
        result.History.Any(h => h.Title == "T_v1").ShouldBeTrue();
    }

    [Fact]
    public void Merge_History_Target_Newer()
    {
        (Database source, Database target) = CreatePair();
        Guid uuid = Guid.NewGuid();

        Entry sourceEntry = new() { Title = "S_v1", Uuid = uuid };
        sourceEntry.Times.LastModificationTime = new DateTime(2025, 2, 1);
        sourceEntry._history.Add(new Entry { Title = "S_v0" });
        source.RootGroup!.AddEntry(sourceEntry);

        Entry targetEntry = new() { Title = "T_v2", Uuid = uuid };
        targetEntry.Times.LastModificationTime = new DateTime(2025, 3, 1);
        targetEntry._history.Add(new Entry { Title = "T_v1" });
        target.RootGroup!.AddEntry(targetEntry);

        Merger.Merge(source, target);

        // Target is newer → keeps target, source becomes history
        Entry result = target.RootGroup.Entries[0];
        result.Title.ShouldBe("T_v2");
        result.History.Count.ShouldBe(3);
        result.History.Any(h => h.Title == "T_v1").ShouldBeTrue();
        result.History.Any(h => h.Title == "S_v0").ShouldBeTrue();
        result.History.Any(h => h.Title == "S_v1").ShouldBeTrue();
    }

    [Fact]
    public void Merge_History_Trimmed_To_MaxItems()
    {
        (Database source, Database target) = CreatePair();
        Guid uuid = Guid.NewGuid();

        target.Metadata!.HistoryMaxItems = 2;

        Entry sourceEntry = new() { Title = "S", Uuid = uuid };
        sourceEntry.Times.LastModificationTime = new DateTime(2025, 4, 1);
        sourceEntry._history.Add(new Entry { Title = "H3" });
        sourceEntry._history.Add(new Entry { Title = "H4" });
        source.RootGroup!.AddEntry(sourceEntry);

        Entry targetEntry = new() { Title = "T", Uuid = uuid };
        targetEntry.Times.LastModificationTime = new DateTime(2025, 3, 1);
        targetEntry._history.Add(new Entry { Title = "H1" });
        targetEntry._history.Add(new Entry { Title = "H2" });
        target.RootGroup!.AddEntry(targetEntry);

        Merger.Merge(source, target);

        target.RootGroup.Entries[0].History.Count.ShouldBeLessThanOrEqualTo(2);
    }

    // ── Dry run ───────────────────────────────────────────────────────────

    [Fact]
    public void Merge_DryRun_Does_Not_Mutate_Target()
    {
        (Database source, Database target) = CreatePair();
        Guid uuid = Guid.NewGuid();

        Entry sourceEntry = new() { Title = "SourceOnly", Uuid = uuid };
        source.RootGroup!.AddEntry(sourceEntry);

        Merger.Merge(source, target, dryRun: true);

        // Target unchanged
        target.RootGroup!.Entries.Count.ShouldBe(0);
    }

    [Fact]
    public void Merge_DryRun_Does_Not_Mutate_Existing_Entry()
    {
        (Database source, Database target) = CreatePair();
        Guid uuid = Guid.NewGuid();

        Entry sourceEntry = new() { Title = "NewTitle", Uuid = uuid };
        sourceEntry.Times.LastModificationTime = new DateTime(2025, 2, 1);
        source.RootGroup!.AddEntry(sourceEntry);

        Entry targetEntry = new() { Title = "OldTitle", Uuid = uuid };
        targetEntry.Times.LastModificationTime = new DateTime(2025, 1, 1);
        target.RootGroup!.AddEntry(targetEntry);

        Merger.Merge(source, target, dryRun: true);

        target.RootGroup.Entries[0].Title.ShouldBe("OldTitle");
    }

    // ── Metadata ──────────────────────────────────────────────────────────

    [Fact]
    public void Merge_Metadata_CustomData()
    {
        (Database source, Database target) = CreatePair();

        source.Metadata!.CustomData ??= new CustomData();
        source.Metadata.CustomData.Set("A", "source_a");
        source.Metadata.CustomData.Set("B", "source_b");

        target.Metadata!.CustomData ??= new CustomData();
        target.Metadata.CustomData.Set("B", "target_b");
        target.Metadata.CustomData.Set("C", "target_c");

        Merger.Merge(source, target);

        target.Metadata.CustomData!.GetValue("A").ShouldBe("source_a");
        target.Metadata.CustomData.GetValue("B").ShouldBe("source_b");
        target.Metadata.CustomData.GetValue("C").ShouldBe("target_c");
    }

    // ── Merge with recycle bin ────────────────────────────────────────────

    [Fact]
    public void Merge_Does_Not_Clone_RecycleBin()
    {
        (Database source, Database target) = CreatePair();

        // Source has a recycle bin with entries
        Group sourceBin = source.GetOrCreateRecycleBin();
        sourceBin.AddEntry(new Entry { Title = "DeletedEntry" });
        source.RootGroup!.AddEntry(new Entry { Title = "ActiveEntry" });

        Merger.Merge(source, target);

        // Target gets active entry but recycle bin is not duplicated
        target.RootGroup!.Entries.Count.ShouldBe(1);
        target.RootGroup.Entries[0].Title.ShouldBe("ActiveEntry");
    }

    // ── Error handling ────────────────────────────────────────────────────

    [Fact]
    public void Merge_Throws_If_No_Root_Group()
    {
        Database noRoot = new(new CompositeKey("pw"));

        Should.Throw<InvalidOperationException>(() =>
            Merger.Merge(noRoot, Database.Create("target"))
        );
    }

    // ── Entry CustomData merge ───────────────────────────────────────────

    [Fact]
    public void Merge_Entry_CustomData_Added_From_Source()
    {
        (Database source, Database target) = CreatePair();
        Guid uuid = Guid.NewGuid();

        Entry sourceEntry = new() { Title = "S", Uuid = uuid };
        sourceEntry.Times.LastModificationTime = new DateTime(2025, 2, 1);
        sourceEntry.CustomData = new CustomData();
        sourceEntry.CustomData.Set("Plugin", "enabled");
        source.RootGroup!.AddEntry(sourceEntry);

        Entry targetEntry = new() { Title = "T", Uuid = uuid };
        targetEntry.Times.LastModificationTime = new DateTime(2025, 1, 1);
        target.RootGroup!.AddEntry(targetEntry);

        Merger.Merge(source, target);

        target.RootGroup.Entries[0].CustomData!.GetValue("Plugin").ShouldBe("enabled");
    }

    // ── Group Synchronize mode ──────────────────────────────────────────

    [Fact]
    public void Merge_Group_Synchronize_Updates_Name()
    {
        (Database source, Database target) = CreatePair();
        Guid groupUuid = Guid.NewGuid();

        Group sourceGroup = new() { Name = "SourceG", Uuid = groupUuid };
        sourceGroup.Times.LastModificationTime = new DateTime(2025, 2, 1);
        sourceGroup.Notes = "FromSource";
        source.RootGroup!.AddGroup(sourceGroup);

        Group targetGroup = new() { Name = "TargetG", Uuid = groupUuid };
        targetGroup.Times.LastModificationTime = new DateTime(2025, 1, 1);
        targetGroup.Notes = "FromTarget";
        target.RootGroup!.AddGroup(targetGroup);

        Merger.Merge(source, target, MergeMode.Synchronize);

        Group result = target.RootGroup.Groups[0];
        // Synchronize: source overwrites regardless of time
        result.Name.ShouldBe("SourceG");
        result.Notes.ShouldBe("FromSource");
    }

    // ── Edge cases ────────────────────────────────────────────────────────

    [Fact]
    public void Merge_Deeply_Nested_Group_Structure()
    {
        (Database source, Database target) = CreatePair();

        Group a = new() { Name = "A" };
        Group b = new() { Name = "B" };
        Group c = new() { Name = "C" };
        c.AddEntry(new Entry { Title = "Deep" });
        b.AddGroup(c);
        a.AddGroup(b);
        source.RootGroup!.AddGroup(a);

        Merger.Merge(source, target);

        Group resultA = target.RootGroup!.Groups[0];
        resultA.Name.ShouldBe("A");
        Group resultB = resultA.Groups[0];
        resultB.Name.ShouldBe("B");
        Group resultC = resultB.Groups[0];
        resultC.Name.ShouldBe("C");
        resultC.Entries[0].Title.ShouldBe("Deep");
    }

    [Fact]
    public void Merge_Entries_In_Multiple_Groups()
    {
        (Database source, Database target) = CreatePair();

        Group g1 = new() { Name = "G1" };
        g1.AddEntry(new Entry { Title = "E1", Uuid = Guid.NewGuid() });
        Group g2 = new() { Name = "G2" };
        g2.AddEntry(new Entry { Title = "E2", Uuid = Guid.NewGuid() });
        source.RootGroup!.AddGroup(g1);
        source.RootGroup.AddGroup(g2);

        Merger.Merge(source, target);

        target.RootGroup!.Groups.Count.ShouldBe(2);
        target.RootGroup.Groups[0].Entries[0].Title.ShouldBe("E1");
        target.RootGroup.Groups[1].Entries[0].Title.ShouldBe("E2");
    }
}
