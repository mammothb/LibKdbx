namespace LibKdbx.Tests;

public class EntryHelperTests
{
    [Fact]
    public void IsRecycled_False_By_Default()
    {
        Entry e = new() { Title = "Test" };
        Group root = new();
        root.AddEntry(e);
        e.IsRecycled().ShouldBeFalse();
    }

    [Fact]
    public void IsRecycled_True_When_In_Recycle_Bin()
    {
        using Database db = Database.Create("pw");
        Guid binUuid = db.Metadata!.RecycleBinUuid;

        Group bin = new() { Uuid = binUuid, Name = "Recycle Bin" };
        Entry e = new() { Title = "Deleted" };
        bin.AddEntry(e);

        // Wire database so IsRecycled can find Metadata
        e.Database = db;

        e.IsRecycled().ShouldBeTrue();
    }

    [Fact]
    public void WillExpireInDays_Not_Expiring()
    {
        Entry e = new() { Times = new Times { Expires = false } };
        e.WillExpireInDays(0).ShouldBeFalse();
        e.WillExpireInDays(10).ShouldBeFalse();
    }

    [Fact]
    public void WillExpireInDays_Already_Expired()
    {
        Entry e = new()
        {
            Times = new Times { Expires = true, ExpiryTime = DateTime.UtcNow.AddDays(-3) },
        };
        e.WillExpireInDays(0).ShouldBeTrue();
        e.WillExpireInDays(5).ShouldBeFalse(); // already expired, not "will expire"
    }

    [Fact]
    public void WillExpireInDays_Within_Range()
    {
        Entry e = new()
        {
            Times = new Times { Expires = true, ExpiryTime = DateTime.UtcNow.AddDays(3) },
        };
        e.WillExpireInDays(5).ShouldBeTrue();
        e.WillExpireInDays(2).ShouldBeFalse();
    }

    [Fact]
    public void HasTotp_With_TotpSeed()
    {
        Entry e = new();
        e.Attributes.Set("TOTP Seed", "JBSWY3DPEHPK3PXP");
        e.HasTotp().ShouldBeTrue();
    }

    [Fact]
    public void HasTotp_With_TotpSettings()
    {
        Entry e = new();
        e.Attributes.Set("TOTP Settings", "30;6");
        e.HasTotp().ShouldBeTrue();
    }

    [Fact]
    public void HasTotp_With_otp()
    {
        Entry e = new();
        e.Attributes.Set("otp", "otpauth://...");
        e.HasTotp().ShouldBeTrue();
    }

    [Fact]
    public void HasTotp_None()
    {
        Entry e = new();
        e.HasTotp().ShouldBeFalse();
    }
}
