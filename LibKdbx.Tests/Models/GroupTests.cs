namespace LibKdbx.Tests;

public class GroupTests
{
    [Fact]
    public void All_Fields_SetAndRead()
    {
        Guid lastTop = Guid.NewGuid();
        Guid prevParent = Guid.NewGuid();

        Group g = new()
        {
            Name = "TestGroup",
            Notes = "notes",
            IconId = 5,
            CustomIconUuid = Guid.NewGuid(),
            IsExpanded = false,
            EnableAutoType = TriState.Disable,
            EnableSearching = TriState.Enable,
            Tags = "tag1;tag2",
            DefaultAutoTypeSequence = "{USERNAME}{TAB}{PASSWORD}",
            LastTopVisibleEntry = lastTop,
            PreviousParentGroup = prevParent,
        };

        g.Name.ShouldBe("TestGroup");
        g.Notes.ShouldBe("notes");
        g.IconId.ShouldBe(5);
        g.CustomIconUuid.ShouldNotBe(Guid.Empty);
        g.IsExpanded.ShouldBeFalse();
        g.EnableAutoType.ShouldBe(TriState.Disable);
        g.EnableSearching.ShouldBe(TriState.Enable);
        g.Tags.ShouldBe("tag1;tag2");
        g.DefaultAutoTypeSequence.ShouldBe("{USERNAME}{TAB}{PASSWORD}");
        g.LastTopVisibleEntry.ShouldBe(lastTop);
        g.PreviousParentGroup.ShouldBe(prevParent);
    }

    [Fact]
    public void Defaults_Are_Sane()
    {
        Group g = new();
        g.Name.ShouldBe("");
        g.EnableAutoType.ShouldBe(TriState.Inherit);
        g.EnableSearching.ShouldBe(TriState.Inherit);
        g.Tags.ShouldBe("");
        g.IsExpanded.ShouldBeTrue();
        g.Uuid.ShouldNotBe(Guid.Empty);
        g.Times.ShouldNotBeNull();
    }

    [Fact]
    public void Clone_Preserves_NewFields()
    {
        Group g = new()
        {
            Name = "Orig",
            Tags = "a;b",
            DefaultAutoTypeSequence = "{TAB}",
            EnableAutoType = TriState.Enable,
            EnableSearching = TriState.Disable,
            LastTopVisibleEntry = Guid.NewGuid(),
            PreviousParentGroup = Guid.NewGuid(),
            CustomData = new CustomData(),
        };
        g.CustomData.Set("x", "y");

        Group clone = g.Clone();

        clone.Name.ShouldBe("Orig");
        clone.Tags.ShouldBe("a;b");
        clone.DefaultAutoTypeSequence.ShouldBe("{TAB}");
        clone.EnableAutoType.ShouldBe(TriState.Enable);
        clone.EnableSearching.ShouldBe(TriState.Disable);
        clone.LastTopVisibleEntry.ShouldBe(g.LastTopVisibleEntry);
        clone.PreviousParentGroup.ShouldBe(g.PreviousParentGroup);
        clone.CustomData.ShouldNotBeNull();
        clone.CustomData.GetValue("x").ShouldBe("y");
        clone.Uuid.ShouldNotBe(g.Uuid); // clone gets new UUID
    }

    [Fact]
    public void CustomData_Is_Null_ByDefault()
    {
        new Group().CustomData.ShouldBeNull();
    }

    // ── CRUD: AddEntry ──────────────────────────────────────────────────

    [Fact]
    public void AddEntry_Throws_When_Already_In_Group()
    {
        Group root = new();
        Group other = new();
        Entry entry = new();
        root.AddEntry(entry);

        Should.Throw<InvalidOperationException>(() => other.AddEntry(entry));
    }

    [Fact]
    public void AddEntry_Without_Database_Does_Not_Throw()
    {
        Group root = new();
        Entry entry = new();
        root.AddEntry(entry);

        entry.ParentGroup.ShouldBe(root);
        entry.Database.ShouldBeNull();
    }

    [Fact]
    public void AddEntry_With_Database_Indexes_And_SetsChanged()
    {
        using Database db = Database.Create("pw");
        Entry entry = new() { Title = "E" };
        db.RootGroup!.AddEntry(entry);

        db.HasChanges.ShouldBeTrue();
        db.FindEntryByUuid(entry.Uuid).ShouldBe(entry);
    }

    // ── CRUD: RemoveEntry ───────────────────────────────────────────────

    [Fact]
    public void RemoveEntry_Throws_When_Not_In_This_Group()
    {
        Group root = new();
        Group other = new();
        Entry entry = new();
        root.AddEntry(entry);

        Should.Throw<InvalidOperationException>(() => other.RemoveEntry(entry));
    }

    [Fact]
    public void RemoveEntry_Without_Database_Does_Not_Throw()
    {
        Group root = new();
        Entry entry = new();
        root.AddEntry(entry);

        root.RemoveEntry(entry);

        entry.ParentGroup.ShouldBeNull();
        root.Entries.Count.ShouldBe(0);
    }

    [Fact]
    public void RemoveEntry_With_Database_Unindexes_And_SetsChanged()
    {
        using Database db = Database.Create("pw");
        Entry entry = new();
        db.RootGroup!.AddEntry(entry);
        db.SaveAs(Path.GetTempFileName()); // reset HasChanges
        try
        {
            db.RootGroup.RemoveEntry(entry);

            db.HasChanges.ShouldBeTrue();
            entry.Database.ShouldBeNull();
            db.FindEntryByUuid(entry.Uuid).ShouldBeNull();
        }
        finally
        {
            File.Delete(db.DatabaseFile!.FullName);
        }
    }

    // ── CRUD: AddGroup ──────────────────────────────────────────────────

    [Fact]
    public void AddGroup_Throws_When_Already_In_Group()
    {
        Group root = new();
        Group other = new();
        Group child = new();
        root.AddGroup(child);

        Should.Throw<InvalidOperationException>(() => other.AddGroup(child));
    }

    [Fact]
    public void AddGroup_Wires_Database_Recursive()
    {
        using Database db = Database.Create("pw");
        Group child = new() { Name = "Child" };
        Entry entry = new();
        child.AddEntry(entry);

        db.RootGroup!.AddGroup(child);

        child.Database.ShouldBe(db);
        entry.Database.ShouldBe(db);
        db.HasChanges.ShouldBeTrue();
    }

    // ── CRUD: RemoveGroup ───────────────────────────────────────────────

    [Fact]
    public void RemoveGroup_Throws_When_Not_In_This_Group()
    {
        Group root = new();
        Group other = new();
        Group child = new();
        root.AddGroup(child);

        Should.Throw<InvalidOperationException>(() => other.RemoveGroup(child));
    }

    [Fact]
    public void RemoveGroup_With_Database_Unindexes_And_Nulls_Db()
    {
        using Database db = Database.Create("pw");
        Group child = new();
        Entry entry = new();
        child.AddEntry(entry);
        db.RootGroup!.AddGroup(child);
        db.SaveAs(Path.GetTempFileName()); // reset HasChanges
        try
        {
            db.RootGroup.RemoveGroup(child);

            db.HasChanges.ShouldBeTrue();
            child.Database.ShouldBeNull();
            entry.Database.ShouldBeNull();
            child.ParentGroup.ShouldBeNull();
        }
        finally
        {
            File.Delete(db.DatabaseFile!.FullName);
        }
    }

    // ── Delete ───────────────────────────────────────────────────────────

    [Fact]
    public void Delete_With_Null_ParentGroup_Does_Nothing()
    {
        Group orphan = new() { Name = "Orphan" };
        orphan.Delete();
        // No exception, no side effects
        orphan.ParentGroup.ShouldBeNull();
    }

    [Fact]
    public void Delete_Without_RecycleBin_Permanently_Removes_From_Parent()
    {
        Group root = new();
        Group child = new();
        root.AddGroup(child);

        child.Delete();

        root.Groups.Count.ShouldBe(0);
        child.ParentGroup.ShouldBeNull();
    }

    [Fact]
    public void Delete_With_RecycleBin_Moves_To_Bin()
    {
        using Database db = Database.Create("pw");
        Group child = new();
        db.RootGroup!.AddGroup(child);

        child.Delete();

        child.ParentGroup!.Name.ShouldBe("Recycle Bin");
        db.RootGroup.Groups.ShouldNotContain(child);
    }

    // ── MoveTo ───────────────────────────────────────────────────────────

    [Fact]
    public void MoveTo_Self_Throws()
    {
        Group root = new();
        Group child = new();
        root.AddGroup(child);

        Should.Throw<InvalidOperationException>(() => child.MoveTo(child));
    }

    [Fact]
    public void MoveTo_Ancestor_Throws()
    {
        Group root = new();
        Group child = new();
        root.AddGroup(child);

        Should.Throw<InvalidOperationException>(() => root.MoveTo(child));
    }

    [Fact]
    public void MoveTo_Updates_Parents_Across_Groups()
    {
        Group root = new();
        Group a = new() { Name = "A" };
        Group b = new() { Name = "B" };
        root.AddGroup(a);
        root.AddGroup(b);

        a.MoveTo(b);

        root.Groups.Count.ShouldBe(1);
        root.Groups[0].ShouldBe(b);
        b.Groups.Count.ShouldBe(1);
        b.Groups[0].ShouldBe(a);
        a.ParentGroup.ShouldBe(b);
    }

    // ── Clone ────────────────────────────────────────────────────────────

    [Fact]
    public void Clone_No_CustomData_Produces_Null_CustomData()
    {
        Group g = new() { Name = "Simple" };
        Group clone = g.Clone();
        clone.CustomData.ShouldBeNull();
    }

    [Fact]
    public void Clone_Nested_Entries_And_Groups()
    {
        Group root = new() { Name = "Root" };
        root.AddEntry(new Entry { Title = "E1" });
        root.AddEntry(new Entry { Title = "E2" });
        Group sub = new() { Name = "Sub" };
        sub.AddEntry(new Entry { Title = "E3" });
        root.AddGroup(sub);

        Group clone = root.Clone();

        clone.Entries.Count.ShouldBe(2);
        clone.Entries[0].Title.ShouldBe("E1");
        clone.Entries[1].Title.ShouldBe("E2");
        clone.Groups.Count.ShouldBe(1);
        clone.Groups[0].Name.ShouldBe("Sub");
        clone.Groups[0].Entries.Count.ShouldBe(1);
        clone.Groups[0].Entries[0].Title.ShouldBe("E3");
    }

    [Fact]
    public void Clone_MergeMode_Preserved()
    {
        Group g = new() { Name = "G", MergeMode = MergeMode.Synchronize };
        Group clone = g.Clone();
        clone.MergeMode.ShouldBe(MergeMode.Synchronize);
    }
}
