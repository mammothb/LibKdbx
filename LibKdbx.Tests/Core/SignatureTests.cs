namespace LibKdbx.Tests;

public class SignatureTests
{
    [Fact]
    public void V4_Matches_KeePassXC()
    {
        // Known KDBX 4.x magic: 0x03D9A29A 0x67FB4BB5
        Signature sig = KdbxHeader.ValidSignature;
        sig.Sign1.ShouldBe(0x9AA2D903u);
        sig.Sign2.ShouldBe(0xB54BFB67u);
    }

    [Fact]
    public void ReadWrite_RoundTrip()
    {
        var original = new Signature(0x9AA2D903, 0xB54BFB67);
        using var ms = new MemoryStream();
        original.Write(ms);
        ms.Position = 0;
        var read = Signature.Read(ms);
        read.ShouldBe(original);
    }

    [Fact]
    public void IsZero()
    {
        new Signature().IsZero.ShouldBeTrue();
        KdbxHeader.ValidSignature.IsZero.ShouldBeFalse();
    }

    [Fact]
    public void Equality()
    {
        var a = new Signature(1, 2);
        var b = new Signature(1, 2);
        var c = new Signature(3, 4);
        a.Equals(b).ShouldBeTrue();
        a.Equals(c).ShouldBeFalse();
        (a == b).ShouldBeTrue();
        (a != c).ShouldBeTrue();
    }

    [Fact]
    public void Equals_Null_Returns_False()
    {
        new Signature(1, 2).Equals(null).ShouldBeFalse();
        new Signature(1, 2).Equals((object?)null).ShouldBeFalse();
    }

    [Fact]
    public void ReadWrite_Stream_Overload()
    {
        var original = new Signature(0xAAAAAAAA, 0xBBBBBBBB);
        using var ms = new MemoryStream();
        original.Write(ms);
        ms.Position = 0;
        var read = Signature.Read(ms);
        read.ShouldBe(original);
    }
}

public class Signature_Kdbx3_Tests
{
    // KDBX 3.x uses the same signature as 4.x (verified against KeePassXC source).
    // KeePass2.cpp uses SIGNATURE_1 = 0x9AA2D903, SIGNATURE_2 = 0xB54BFB67 for both.
    // The difference is in Version: (3,1) vs (4,x) — not in the signature.
    [Fact]
    public void Same_Signature_For_Both_Versions()
    {
        // Both KDBX 3.x and 4.x use identical magic bytes
        Signature v4Sig = KdbxHeader.ValidSignature;
        // There is no separate V3 signature — just verify V4 is correct
        v4Sig.Sign1.ShouldBe(0x9AA2D903u);
        v4Sig.Sign2.ShouldBe(0xB54BFB67u);
    }
}
