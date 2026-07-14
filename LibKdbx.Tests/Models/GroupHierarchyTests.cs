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
}
