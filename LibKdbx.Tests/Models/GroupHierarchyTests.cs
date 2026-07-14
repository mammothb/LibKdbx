namespace LibKdbx.Tests;

public class GroupHierarchyTests
{
    [Fact]
    public void GroupsRecursive_Includes_Self()
    {
        Group root = new() { Name = "Root" };
        List<Group> all = root.GroupsRecursive(true);
        all.Count.ShouldBe(1);
        all[0].ShouldBe(root);
    }

    [Fact]
    public void GroupsRecursive_Excludes_Self()
    {
        Group root = new() { Name = "Root" };
        Group child = new() { Name = "Child" };
        root.AddGroup(child);

        List<Group> result = root.GroupsRecursive(false);
        result.Count.ShouldBe(1);
        result[0].ShouldBe(child);
    }

    [Fact]
    public void GroupsRecursive_Nested()
    {
        Group root = new() { Name = "Root" };
        Group a = new() { Name = "A" };
        Group b = new() { Name = "B" };
        Group aa = new() { Name = "AA" };
        root.AddGroup(a);
        root.AddGroup(b);
        a.AddGroup(aa);

        List<Group> all = root.GroupsRecursive(true);
        all.Count.ShouldBe(4);
    }

    [Fact]
    public void Hierarchy_From_Root()
    {
        Group root = new() { Name = "Root" };
        List<string> path = root.Hierarchy();
        path.Count.ShouldBe(1);
        path[0].ShouldBe("Root");
    }

    [Fact]
    public void Hierarchy_Nested()
    {
        Group root = new() { Name = "Root" };
        Group work = new() { Name = "Work" };
        Group dev = new() { Name = "Dev" };
        root.AddGroup(work);
        work.AddGroup(dev);

        List<string> path = dev.Hierarchy();
        path.Count.ShouldBe(3);
        path[0].ShouldBe("Root");
        path[1].ShouldBe("Work");
        path[2].ShouldBe("Dev");
    }

    [Fact]
    public void ResolveSearchingEnabled_Default()
    {
        Group group = new();
        group.ResolveSearchingEnabled().ShouldBeTrue();
    }

    [Fact]
    public void ResolveSearchingEnabled_Disabled()
    {
        Group group = new() { EnableSearching = TriState.Disable };
        group.ResolveSearchingEnabled().ShouldBeFalse();
    }

    [Fact]
    public void ResolveSearchingEnabled_Enabled_Overrides_Parent_Disabled()
    {
        Group parent = new() { EnableSearching = TriState.Disable };
        Group child = new() { EnableSearching = TriState.Enable };
        parent.AddGroup(child);

        child.ResolveSearchingEnabled().ShouldBeTrue();
    }

    [Fact]
    public void ResolveSearchingEnabled_Inherits_Parent_Disabled()
    {
        Group parent = new() { EnableSearching = TriState.Disable };
        Group child = new() { EnableSearching = TriState.Inherit };
        parent.AddGroup(child);

        child.ResolveSearchingEnabled().ShouldBeFalse();
    }

    // ── SortChildrenRecursively ───────────────────────────────────────

    [Fact]
    public void SortChildren_Empty_Group_Does_Nothing()
    {
        var group = new Group();
        group.SortChildrenRecursively();
        group.Groups.Count.ShouldBe(0);
    }

    [Fact]
    public void SortChildren_Alphabetical_Order()
    {
        var root = new Group { Name = "Root" };
        root.AddGroup(new Group { Name = "Zulu" });
        root.AddGroup(new Group { Name = "Alpha" });
        root.AddGroup(new Group { Name = "Charlie" });

        root.SortChildrenRecursively();

        root.Groups.Select(g => g.Name).ShouldBe(["Alpha", "Charlie", "Zulu"]);
    }

    [Fact]
    public void SortChildren_Reverse_Order()
    {
        var root = new Group { Name = "Root" };
        root.AddGroup(new Group { Name = "Alpha" });
        root.AddGroup(new Group { Name = "Zulu" });
        root.AddGroup(new Group { Name = "Charlie" });

        root.SortChildrenRecursively(reverse: true);

        root.Groups.Select(g => g.Name).ShouldBe(["Zulu", "Charlie", "Alpha"]);
    }

    [Fact]
    public void SortChildren_RecycleBin_Last()
    {
        using Database db = Database.Create("pw");
        db.GetOrCreateRecycleBin();

        Group root = db.RootGroup!;
        root.AddGroup(new Group { Name = "Alpha" });
        root.AddGroup(new Group { Name = "Zulu" });

        root.SortChildrenRecursively();

        root.Groups.Select(g => g.Name).ShouldBe(["Alpha", "Zulu", "Recycle Bin"]);
    }

    [Fact]
    public void SortChildren_Nested_Groups()
    {
        var root = new Group { Name = "Root" };
        Group b = new() { Name = "B" };
        Group a = new() { Name = "A" };
        b.AddGroup(new Group { Name = "B2" });
        b.AddGroup(new Group { Name = "B1" });
        a.AddGroup(new Group { Name = "A2" });
        a.AddGroup(new Group { Name = "A1" });
        root.AddGroup(b);
        root.AddGroup(a);

        root.SortChildrenRecursively();

        root.Groups.Select(g => g.Name).ShouldBe(["A", "B"]);
        a.Groups.Select(g => g.Name).ShouldBe(["A1", "A2"]);
        b.Groups.Select(g => g.Name).ShouldBe(["B1", "B2"]);
    }

    [Fact]
    public void SortChildren_Does_Not_Sort_Entries()
    {
        var root = new Group { Name = "Root" };
        root.AddEntry(new Entry { Title = "Z" });
        root.AddEntry(new Entry { Title = "A" });
        root.AddGroup(new Group { Name = "B" });
        root.AddGroup(new Group { Name = "A" });

        root.SortChildrenRecursively();

        root.Groups.Select(g => g.Name).ShouldBe(["A", "B"]);
        root.Entries.Select(e => e.Title).ShouldBe(["Z", "A"]);
    }

    [Fact]
    public void SortChildren_CaseInsensitive()
    {
        var root = new Group { Name = "Root" };
        root.AddGroup(new Group { Name = "zebra" });
        root.AddGroup(new Group { Name = "Alpha" });
        root.AddGroup(new Group { Name = "beta" });

        root.SortChildrenRecursively();

        root.Groups.Select(g => g.Name).ShouldBe(["Alpha", "beta", "zebra"]);
    }

    // ── IsAncestorOf ─────────────────────────────────────────────────

    [Fact]
    public void IsAncestorOf_True_For_Direct_Parent()
    {
        Group root = new();
        Group child = new();
        root.AddGroup(child);

        root.IsAncestorOf(child).ShouldBeTrue();
    }

    [Fact]
    public void IsAncestorOf_True_For_Grandparent()
    {
        Group root = new();
        Group child = new();
        Group grandchild = new();
        root.AddGroup(child);
        child.AddGroup(grandchild);

        root.IsAncestorOf(grandchild).ShouldBeTrue();
    }

    [Fact]
    public void IsAncestorOf_False_For_Unrelated()
    {
        Group a = new();
        Group b = new();

        a.IsAncestorOf(b).ShouldBeFalse();
    }

    [Fact]
    public void IsAncestorOf_False_For_Self()
    {
        Group root = new();
        root.IsAncestorOf(root).ShouldBeFalse();
    }

    [Fact]
    public void IsAncestorOf_False_For_Child_Of_Child()
    {
        Group root = new();
        Group child = new();
        root.AddGroup(child);

        child.IsAncestorOf(root).ShouldBeFalse();
    }

    // ── FindEntry ────────────────────────────────────────────────────

    [Fact]
    public void FindEntry_By_Title_Returns_Match()
    {
        Group root = new();
        root.AddEntry(new Entry { Title = "Alpha" });
        root.AddEntry(new Entry { Title = "Beta" });

        Entry? found = root.FindEntry("Beta");
        found.ShouldNotBeNull();
        found.Title.ShouldBe("Beta");
    }

    [Fact]
    public void FindEntry_By_Title_Returns_Null_For_Miss()
    {
        Group root = new();
        root.AddEntry(new Entry { Title = "Alpha" });

        root.FindEntry("Nope").ShouldBeNull();
    }

    [Fact]
    public void FindEntry_By_Predicate_Finds_In_Subgroup()
    {
        Group root = new();
        Group sub = new();
        root.AddGroup(sub);
        sub.AddEntry(new Entry { Title = "Deep" });

        Entry? found = root.FindEntry(e => e.Title == "Deep");
        found.ShouldNotBeNull();
        found.Title.ShouldBe("Deep");
    }

    [Fact]
    public void FindEntry_By_Predicate_Returns_Null_When_No_Match()
    {
        Group root = new();
        root.AddEntry(new Entry { Title = "Only" });

        root.FindEntry(e => e.Title == "Missing").ShouldBeNull();
    }

    // ── FindAllEntries ───────────────────────────────────────────────

    [Fact]
    public void FindAllEntries_Matches_Across_Levels()
    {
        Group root = new();
        root.AddEntry(new Entry { UserName = "alice" });
        Group sub = new();
        sub.AddEntry(new Entry { UserName = "alice" });
        root.AddGroup(sub);

        List<Entry> results = [.. root.FindAllEntries(e => e.UserName == "alice")];
        results.Count.ShouldBe(2);
    }

    [Fact]
    public void FindAllEntries_No_Match_Returns_Empty()
    {
        Group root = new();
        root.AddEntry(new Entry { Title = "A" });

        root.FindAllEntries(e => e.Title == "Z").ShouldBeEmpty();
    }

    // ── FindGroup ────────────────────────────────────────────────────

    [Fact]
    public void FindGroup_By_Name_Nested()
    {
        Group root = new();
        Group a = new() { Name = "A" };
        Group b = new() { Name = "B" };
        Group aa = new() { Name = "AA" };
        root.AddGroup(a);
        root.AddGroup(b);
        a.AddGroup(aa);

        root.FindGroup("AA").ShouldBe(aa);
    }

    [Fact]
    public void FindGroup_By_Name_Returns_Null_For_Miss()
    {
        Group root = new();
        root.AddGroup(new Group { Name = "Work" });

        root.FindGroup("Personal").ShouldBeNull();
    }

    [Fact]
    public void FindGroup_By_Predicate()
    {
        Group root = new();
        root.AddGroup(new Group { Name = "Work", Notes = "office" });
        root.AddGroup(new Group { Name = "Personal", Notes = "home" });

        Group? found = root.FindGroup(g => g.Notes == "home");
        found.ShouldNotBeNull();
        found.Name.ShouldBe("Personal");
    }

    [Fact]
    public void FindGroup_By_Predicate_Returns_Null_When_No_Match()
    {
        Group root = new();
        root.AddGroup(new Group { Name = "Work" });

        root.FindGroup(g => g.Name == "Missing").ShouldBeNull();
    }

    // ── FindAllGroups ────────────────────────────────────────────────

    [Fact]
    public void FindAllGroups_Matches_Nested()
    {
        Group root = new();
        Group a = new() { Tags = "shared" };
        Group b = new() { Tags = "shared" };
        Group aa = new() { Tags = "unique" };
        root.AddGroup(a);
        root.AddGroup(b);
        a.AddGroup(aa);

        List<Group> results = [.. root.FindAllGroups(g => g.Tags == "shared")];
        results.Count.ShouldBe(2);
        results.ShouldContain(a);
        results.ShouldContain(b);
    }

    [Fact]
    public void FindAllGroups_No_Match_Returns_Empty()
    {
        Group root = new();
        root.AddGroup(new Group { Name = "A" });

        root.FindAllGroups(g => g.Name == "Z").ShouldBeEmpty();
    }

    // ── Hierarchy ────────────────────────────────────────────────────

    [Fact]
    public void Hierarchy_Standalone_Group()
    {
        Group orphan = new() { Name = "Orphan" };
        List<string> path = orphan.Hierarchy();
        path.Count.ShouldBe(1);
        path[0].ShouldBe("Orphan");
    }

    // ── ResolveSearchingEnabled ───────────────────────────────────────

    [Fact]
    public void ResolveSearchingEnabled_TopLevel_Null_Parent_Defaults_True()
    {
        Group g = new() { EnableSearching = TriState.Inherit };
        g.ResolveSearchingEnabled().ShouldBeTrue();
    }

    // ── SetDatabaseRecursive ─────────────────────────────────────────

    [Fact]
    public void SetDatabaseRecursive_Propagates_To_Entries_And_Subgroups()
    {
        using Database db = Database.Create("pw");
        Group root = new();
        Entry e = new();
        Group sub = new();
        Entry subEntry = new();
        root.AddEntry(e);
        sub.AddEntry(subEntry);
        root.AddGroup(sub);

        root.SetDatabaseRecursive(db);

        root.Database.ShouldBe(db);
        e.Database.ShouldBe(db);
        sub.Database.ShouldBe(db);
        subEntry.Database.ShouldBe(db);
    }

    [Fact]
    public void SetDatabaseRecursive_Null_Clears_All()
    {
        using Database db = Database.Create("pw");
        Group child = new();
        Entry entry = new();
        child.AddEntry(entry);
        db.RootGroup!.AddGroup(child);

        child.SetDatabaseRecursive(null);

        child.Database.ShouldBeNull();
        entry.Database.ShouldBeNull();
    }
}
