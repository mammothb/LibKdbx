using System.Security.Cryptography;

namespace LibKdbx.Tests;

public class DerivedKeyTests
{
    [Fact]
    public void Derive_Produces_32ByteKey()
    {
        byte[] seed = new byte[16];
        RandomNumberGenerator.Fill(seed);
        AesKdf kdf = new(seed, 1000);

        using var ck = new CompositeKey("test");
        DerivedKey dk = DerivedKey.Derive(ck, kdf);

        dk.GetRawKey().Length.ShouldBe(32);
    }

    [Fact]
    public void Derive_SameInputs_Produces_SameOutput()
    {
        byte[] seed = new byte[16];
        RandomNumberGenerator.Fill(seed);
        AesKdf kdf = new(seed, 1000);

        using var ck1 = new CompositeKey("test");
        using var ck2 = new CompositeKey("test");

        DerivedKey dk1 = DerivedKey.Derive(ck1, kdf);
        DerivedKey dk2 = DerivedKey.Derive(ck2, kdf);

        dk1.GetRawKey().ShouldBe(dk2.GetRawKey());
    }

    [Fact]
    public void Derive_DifferentPasswords_Produces_DifferentOutput()
    {
        byte[] seed = new byte[16];
        RandomNumberGenerator.Fill(seed);
        AesKdf kdf = new(seed, 1000);

        using var ck1 = new CompositeKey("password1");
        using var ck2 = new CompositeKey("password2");

        DerivedKey dk1 = DerivedKey.Derive(ck1, kdf);
        DerivedKey dk2 = DerivedKey.Derive(ck2, kdf);

        dk1.GetRawKey().ShouldNotBe(dk2.GetRawKey());
    }
}
