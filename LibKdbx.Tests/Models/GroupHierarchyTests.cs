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
}
