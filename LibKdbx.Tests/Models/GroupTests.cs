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
}
