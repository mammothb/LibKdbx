using System.Security.Cryptography;

namespace LibKdbx.Tests;

public class Argon2KdfTests
{
    [Fact]
    public void Parameter_RoundTrip_Argon2id()
    {
        byte[] salt = new byte[32];
        RandomNumberGenerator.Fill(salt);

        Argon2Kdf kdf = new(salt, parallelism: 4, memoryKib: 65536, iterations: 3, Argon2Type.Id);
        VariantMap map = kdf.Parameters();

        byte[] serialized = map.Serialize();
        VariantMap deserialized = VariantMap.Read(serialized);

        ((byte[])deserialized["S"]).ShouldBe(salt);
        ((uint)deserialized["P"]).ShouldBe((uint)4);
        ((ulong)deserialized["M"]).ShouldBe((ulong)(65536 * 1024L));
        ((ulong)deserialized["I"]).ShouldBe((ulong)3);
    }

    [Fact]
    public void Parameter_RoundTrip_Argon2d()
    {
        byte[] salt = new byte[32];
        RandomNumberGenerator.Fill(salt);

        Argon2Kdf kdf = new(salt, parallelism: 2, memoryKib: 32768, iterations: 2, Argon2Type.D);
        VariantMap map = kdf.Parameters();

        byte[] serialized = map.Serialize();
        VariantMap deserialized = VariantMap.Read(serialized);

        // UUID should match Argon2d
        byte[] expectedUuid = GuidRfc4122.ToBytes(Argon2Kdf.Argon2dUuid);
        ((byte[])deserialized["$UUID"]).ShouldBe(expectedUuid);
    }

    [Fact]
    public void Produces_32_Byte_Key()
    {
        byte[] salt = new byte[32];
        RandomNumberGenerator.Fill(salt);
        byte[] input = new byte[32];
        RandomNumberGenerator.Fill(input);

        Argon2Kdf kdf = new(salt, parallelism: 2, memoryKib: 16384, iterations: 2);

        byte[] result = kdf.Transform(input);
        result.Length.ShouldBe(32);
    }

    [Fact]
    public void Deterministic_Output()
    {
        byte[] salt = new byte[32];
        RandomNumberGenerator.Fill(salt);
        byte[] input = new byte[32];
        RandomNumberGenerator.Fill(input);

        Argon2Kdf kdf1 = new(salt, 2, 16384, 2);
        Argon2Kdf kdf2 = new(salt, 2, 16384, 2);

        byte[] result1 = kdf1.Transform(input);
        byte[] result2 = kdf2.Transform(input);

        result1.ShouldBe(result2);
    }
}
