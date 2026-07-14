namespace LibKdbx.Tests;

public class EntryTests
{
    [Fact]
    public void All_Fields_SetAndRead()
    {
        Guid prevParent = Guid.NewGuid();

        Entry e = new()
        {
            IconId = 3,
            CustomIconUuid = Guid.NewGuid(),
            ForegroundColor = "#000",
            BackgroundColor = "#FFF",
            OverrideUrl = "https://override.example.com",
            Tags = "work;important",
            PreviousParentGroup = prevParent,
            ExcludeFromReports = true,
        };
        e.Attributes.Title = "T";
        e.Attributes.UserName = "U";
        e.Attributes.Password = "P";
        e.Attributes.Url = "https://example.com";
        e.Attributes.Notes = "N";

        e.IconId.ShouldBe(3);
        e.CustomIconUuid.ShouldNotBe(Guid.Empty);
        e.ForegroundColor.ShouldBe("#000");
        e.BackgroundColor.ShouldBe("#FFF");
        e.OverrideUrl.ShouldBe("https://override.example.com");
        e.Tags.ShouldBe("work;important");
        e.PreviousParentGroup.ShouldBe(prevParent);
        e.ExcludeFromReports.ShouldBeTrue();
        e.Title.ShouldBe("T");
        e.UserName.ShouldBe("U");
        e.Password.ShouldBe("P");
        e.Url.ShouldBe("https://example.com");
        e.Notes.ShouldBe("N");
    }

    [Fact]
    public void Defaults_Are_Sane()
    {
        Entry e = new();
        e.Title.ShouldBe("");
        e.Password.ShouldBe("");
        e.ExcludeFromReports.ShouldBeFalse();
        e.PreviousParentGroup.ShouldBe(Guid.Empty);
        e.Attributes.ShouldNotBeNull();
        e.Attachments.ShouldNotBeNull();
        e.AutoType.Enabled.ShouldBeTrue();
    }

    [Fact]
    public void DeepCopy_Preserves_NewFields()
    {
        Entry orig = new()
        {
            Tags = "a;b",
            PreviousParentGroup = Guid.NewGuid(),
            ExcludeFromReports = true,
        };
        orig.Attributes.Title = "Original";
        orig.Attributes.Set("CustomKey", "CustomVal", protect: true);
        orig.Attachments.Set("file.txt", [1, 2, 3]);
        orig.CustomData = new CustomData();
        orig.CustomData.Set("cd", "val");

        // Use Update() which calls DeepCopy() internally
        orig.Update(e =>
        {
            e.Title = "Modified";
        });

        // The history snapshot should have the original values
        Entry snapshot = orig.History[0];
        snapshot.Title.ShouldBe("Original");
        snapshot.Tags.ShouldBe("a;b");
        snapshot.PreviousParentGroup.ShouldBe(orig.PreviousParentGroup);
        snapshot.ExcludeFromReports.ShouldBeTrue();
        snapshot.Attributes.Get("CustomKey").ShouldBe("CustomVal");
        snapshot.Attributes.IsProtected("CustomKey").ShouldBeTrue();
        snapshot.Attachments.Get("file.txt").ShouldBe([1, 2, 3]);
        snapshot.CustomData.ShouldNotBeNull();
        snapshot.CustomData.GetValue("cd").ShouldBe("val");

        // Current entry has modified title
        orig.Title.ShouldBe("Modified");
    }

    [Fact]
    public void Clone_Is_Independent()
    {
        Entry orig = new()
        {
            Title = "Original",
            Tags = "tag",
            ExcludeFromReports = true,
        };
        orig.Attributes.Set("Custom", "value");
        orig.Attachments.Set("file.txt", [1, 2]);

        Entry clone = orig.Clone();

        clone.Title = "Cloned";
        clone.Attributes.Set("Custom", "changed");
        clone.Attachments.Set("file.txt", [9, 9]);

        orig.Title.ShouldBe("Original");
        orig.Attributes.Get("Custom").ShouldBe("value");
        orig.Attachments.Get("file.txt").ShouldBe([1, 2]);
    }

    [Fact]
    public void AutoType_Clone_Is_Independent()
    {
        AutoType orig = new()
        {
            Enabled = false,
            DataTransferObfuscation = 2,
            DefaultSequence = "{USERNAME}{TAB}{PASSWORD}",
        };
        orig.Associations.Add(
            new AutoTypeAssociation { Window = "Firefox", Sequence = "{PASSWORD}{ENTER}" }
        );

        AutoType clone = orig.Clone();

        clone.Enabled = true;
        clone.Associations[0].Window = "Chrome";

        orig.Enabled.ShouldBeFalse();
        orig.Associations[0].Window.ShouldBe("Firefox");
        clone.Enabled.ShouldBeTrue();
        clone.Associations[0].Window.ShouldBe("Chrome");
    }

    [Fact]
    public void CustomData_Is_Null_ByDefault()
    {
        new Entry().CustomData.ShouldBeNull();
    }

    // ── Delete / MoveTo ─────────────────────────────────────────────────

    [Fact]
    public void Delete_Null_ParentGroup_Does_Nothing()
    {
        Entry orphan = new() { Title = "Orphan" };
        orphan.Delete();
        orphan.ParentGroup.ShouldBeNull();
    }

    [Fact]
    public void MoveTo_Updates_ParentGroup()
    {
        Group root = new();
        Group target = new();
        Entry entry = new() { Title = "Moved" };
        root.AddEntry(entry);

        entry.MoveTo(target);

        entry.ParentGroup.ShouldBe(target);
        root.Entries.Count.ShouldBe(0);
        target.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public void IsRecycled_Null_Database_Returns_False()
    {
        Group root = new();
        Entry entry = new();
        root.AddEntry(entry);
        entry.IsRecycled().ShouldBeFalse();
    }

    [Fact]
    public void IsRecycled_Null_ParentGroup_Returns_False()
    {
        Entry entry = new();
        entry.IsRecycled().ShouldBeFalse();
    }
}
