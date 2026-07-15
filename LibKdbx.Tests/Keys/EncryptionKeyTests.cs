using System.Security.Cryptography;

namespace LibKdbx.Tests;

public class EncryptionKeyTests
{
    [Fact]
    public void GetKey_Returns_32Bytes()
    {
        byte[] masterSeed = CryptoHelpers.GetRandomBytes(32);

        using var ck = new CompositeKey("hunter2");
        byte[] seed = CryptoHelpers.GetRandomBytes(16);
        AesKdf kdf = new(seed, 1000);
        DerivedKey dk = DerivedKey.Derive(ck, kdf);

        EncryptionKey ek = new(masterSeed, dk);
        ek.GetKey().Length.ShouldBe(32);
    }

    [Fact]
    public void GetHmacKey_Returns_64Bytes()
    {
        byte[] masterSeed = CryptoHelpers.GetRandomBytes(32);

        using var ck = new CompositeKey("hunter2");
        byte[] seed = CryptoHelpers.GetRandomBytes(16);
        AesKdf kdf = new(seed, 1000);
        DerivedKey dk = DerivedKey.Derive(ck, kdf);

        EncryptionKey ek = new(masterSeed, dk);
        ek.GetHmacKey().Length.ShouldBe(64);
    }

    [Fact]
    public void Deterministic_Given_SameInputs()
    {
        byte[] masterSeed = CryptoHelpers.GetRandomBytes(32);

        using var ck = new CompositeKey("test");
        byte[] seed = CryptoHelpers.GetRandomBytes(16);
        AesKdf kdf = new(seed, 1000);
        DerivedKey dk = DerivedKey.Derive(ck, kdf);

        EncryptionKey ek1 = new(masterSeed, dk);

        // Re-derive with same inputs
        using var ck2 = new CompositeKey("test");
        DerivedKey dk2 = DerivedKey.Derive(ck2, new AesKdf(seed, 1000));
        EncryptionKey ek2 = new(masterSeed, dk2);

        ek1.GetKey().ShouldBe(ek2.GetKey());
        ek1.GetHmacKey().ShouldBe(ek2.GetHmacKey());
    }

    [Fact]
    public void Different_MasterSeed_Different_Keys()
    {
        byte[] ms1 = CryptoHelpers.GetRandomBytes(32);
        byte[] ms2 = CryptoHelpers.GetRandomBytes(32);
        ms2.ShouldNotBe(ms1);

        using var ck = new CompositeKey("test");
        byte[] seed = CryptoHelpers.GetRandomBytes(16);
        AesKdf kdf = new(seed, 1000);
        DerivedKey dk = DerivedKey.Derive(ck, kdf);

        EncryptionKey ek1 = new(ms1, dk);
        EncryptionKey ek2 = new(ms2, dk);

        ek1.GetKey().ShouldNotBe(ek2.GetKey());
    }
}
