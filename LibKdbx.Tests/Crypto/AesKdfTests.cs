using System.Security.Cryptography;

namespace LibKdbx.Tests;

public class AesKdfTests
{
    [Fact]
    public void Parameter_RoundTrip()
    {
        byte[] seed = CryptoHelpers.GetRandomBytes(16);
        const ulong rounds = 100_000;

        AesKdf kdf = new(seed, rounds);
        VariantMap map = kdf.Parameters();

        // Serialize → deserialize → verify
        byte[] serialized = map.Serialize();
        VariantMap deserialized = VariantMap.Read(serialized);

        ((byte[])deserialized["S"]).ShouldBe(seed);
        ((ulong)deserialized["R"]).ShouldBe(rounds);
    }

    [Fact]
    public void Deterministic_Output()
    {
        byte[] seed = CryptoHelpers.GetRandomBytes(16);
        byte[] input = CryptoHelpers.GetRandomBytes(32);

        AesKdf kdf1 = new(seed, 1000);
        AesKdf kdf2 = new(seed, 1000);

        byte[] result1 = kdf1.Transform(input);
        byte[] result2 = kdf2.Transform(input);

        result1.ShouldBe(result2);
    }

    [Fact]
    public void Different_Rounds_Produces_Different_Output()
    {
        byte[] seed = CryptoHelpers.GetRandomBytes(16);
        byte[] input = CryptoHelpers.GetRandomBytes(32);

        AesKdf kdf1 = new(seed, 100);
        AesKdf kdf2 = new(seed, 101);

        byte[] result1 = kdf1.Transform(input);
        byte[] result2 = kdf2.Transform(input);

        result1.ShouldNotBe(result2);
    }
}
