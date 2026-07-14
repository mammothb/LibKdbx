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

    // ── IsZero ───────────────────────────────────────────────────────

    [Fact]
    public void IsZero_True_For_Default()
    {
        new Version().IsZero.ShouldBeTrue();
    }

    [Fact]
    public void IsZero_False_For_NonZero()
    {
        new Version(4, 1).IsZero.ShouldBeFalse();
    }

    // ── CompareTo null ───────────────────────────────────────────────

    [Fact]
    public void CompareTo_Null_Returns_1()
    {
        new Version(4, 1).CompareTo(null).ShouldBe(1);
    }

    // ── Equals null ──────────────────────────────────────────────────

    [Fact]
    public void Equals_Null_Returns_False()
    {
        new Version(4, 1).Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Equals_Object_Null_Returns_False()
    {
        new Version(4, 1).Equals((object?)null).ShouldBeFalse();
    }

    // ── Operators ────────────────────────────────────────────────────

    [Fact]
    public void Operators_GreaterThan_And_LessThan()
    {
        Version a = new(4, 0);
        Version b = new(3, 1);
        (a > b).ShouldBeTrue();
        (a >= b).ShouldBeTrue();
        (b < a).ShouldBeTrue();
        (b <= a).ShouldBeTrue();
        (a < b).ShouldBeFalse();
        (b > a).ShouldBeFalse();
        (new Version(3, 1) >= new Version(3, 1)).ShouldBeTrue();
        (new Version(3, 1) <= new Version(3, 1)).ShouldBeTrue();
    }

    // ── Null operators ───────────────────────────────────────────────

    [Fact]
    public void Nullable_Operators()
    {
        Version? v = new(4, 1);
        (v == null).ShouldBeFalse();
        (v != null).ShouldBeTrue();
        Version? n = null;
        (n == null).ShouldBeTrue();
        (n != null).ShouldBeFalse();
    }

    // ── Read/Write Stream overloads ──────────────────────────────────

    [Fact]
    public void ReadWrite_Stream_Overloads()
    {
        var original = new Version(2, 7);
        using var ms = new MemoryStream();
        original.Write(ms);
        ms.Position = 0;
        var read = Version.Read(ms);
        read.ShouldBe(original);
    }
}
