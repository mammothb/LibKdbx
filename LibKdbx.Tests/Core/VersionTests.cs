namespace LibKdbx.Tests;

public class VersionTests
{
    [Fact]
    public void Parse_MajorMinor()
    {
        var v = new Version(4, 1);
        v.Major.ShouldBe((ushort)4);
        v.Minor.ShouldBe((ushort)1);

        var v3 = new Version(3, 1);
        v3.Major.ShouldBe((ushort)3);
        v3.Minor.ShouldBe((ushort)1);
    }

    [Fact]
    public void ReadWrite_RoundTrip()
    {
        var original = new Version(3, 1);
        using var ms = new MemoryStream();
        original.Write(ms);
        ms.Position = 0;
        var read = Version.Read(ms);
        read.ShouldBe(original);
    }

    [Fact]
    public void Comparison()
    {
        (new Version(4, 0) > new Version(3, 1)).ShouldBeTrue();
        (new Version(3, 2) > new Version(3, 1)).ShouldBeTrue();
        (new Version(3, 1) == new Version(3, 1)).ShouldBeTrue();
        (new Version(3, 1) != new Version(4, 0)).ShouldBeTrue();
    }

    [Fact]
    public void ToString_Format()
    {
        new Version(4, 1).ToString().ShouldBe("4.1");
    }
}
