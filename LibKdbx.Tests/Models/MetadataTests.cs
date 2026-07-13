namespace LibKdbx.Tests;

public class MetadataTests
{
    [Fact]
    public void All_Fields_SetAndRead()
    {
        Metadata m = new()
        {
            Generator = "TestGen",
            Name = "db",
            NameChanged = DateTime.UnixEpoch,
            Description = "desc",
            DescriptionChanged = DateTime.UnixEpoch.AddDays(1),
            DefaultUserName = "alice",
            DefaultUserNameChanged = DateTime.UnixEpoch.AddDays(2),
            MaintenanceHistoryDays = 42,
            Color = "#FF0000",
            RecycleBinEnabled = false,
            RecycleBinUuid = Guid.NewGuid(),
            RecycleBinChanged = DateTime.UnixEpoch.AddDays(3),
            HistoryMaxItems = 20,
            HistoryMaxSize = 1_000_000,
            ProtectTitle = true,
            ProtectUserName = true,
            ProtectPassword = false,
            ProtectUrl = true,
            ProtectNotes = false,
            MasterKeyChangeRec = 5,
            MasterKeyChangeForce = 10,
            EntryTemplatesGroup = Guid.NewGuid(),
            EntryTemplatesGroupChanged = DateTime.UnixEpoch.AddDays(4),
            LastSelectedGroup = Guid.NewGuid(),
            LastTopVisibleGroup = Guid.NewGuid(),
        };

        m.Generator.ShouldBe("TestGen");
        m.Name.ShouldBe("db");
        m.NameChanged.ShouldBe(DateTime.UnixEpoch);
        m.Description.ShouldBe("desc");
        m.DescriptionChanged.ShouldBe(DateTime.UnixEpoch.AddDays(1));
        m.DefaultUserName.ShouldBe("alice");
        m.DefaultUserNameChanged.ShouldBe(DateTime.UnixEpoch.AddDays(2));
        m.MaintenanceHistoryDays.ShouldBe(42);
        m.Color.ShouldBe("#FF0000");
        m.RecycleBinEnabled.ShouldBeFalse();
        m.RecycleBinUuid.ShouldNotBe(Guid.Empty);
        m.RecycleBinChanged.ShouldBe(DateTime.UnixEpoch.AddDays(3));
        m.HistoryMaxItems.ShouldBe(20);
        m.HistoryMaxSize.ShouldBe(1_000_000);
        m.ProtectTitle.ShouldBeTrue();
        m.ProtectUserName.ShouldBeTrue();
        m.ProtectPassword.ShouldBeFalse();
        m.ProtectUrl.ShouldBeTrue();
        m.ProtectNotes.ShouldBeFalse();
        m.MasterKeyChangeRec.ShouldBe(5);
        m.MasterKeyChangeForce.ShouldBe(10);
        m.EntryTemplatesGroup.ShouldNotBe(Guid.Empty);
        m.EntryTemplatesGroupChanged.ShouldBe(DateTime.UnixEpoch.AddDays(4));
        m.LastSelectedGroup.ShouldNotBe(Guid.Empty);
        m.LastTopVisibleGroup.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Generator_Defaults_To_MmbKeePass()
    {
        new Metadata().Generator.ShouldBe("LibKdbx");
    }

    [Fact]
    public void CustomData_Is_Null_ByDefault()
    {
        new Metadata().CustomData.ShouldBeNull();
    }

    [Fact]
    public void CustomData_Wired()
    {
        Metadata m = new()
        {
            CustomData = new CustomData()
        };
        m.CustomData.Set("key", "value");
        m.CustomData.GetValue("key").ShouldBe("value");
    }
}
