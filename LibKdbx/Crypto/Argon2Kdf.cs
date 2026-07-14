using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace LibKdbx;

public enum Argon2Type
{
    D,
    Id,
}

/// <summary>
/// Argon2 key derivation function (Argon2d or Argon2id) via BouncyCastle.
/// </summary>
public class Argon2Kdf(
    byte[] salt,
    int parallelism,
    int memoryKib,
    int iterations,
    Argon2Type type = Argon2Type.Id
) : IKdf
{
    public static readonly Guid Argon2dUuid = new("ef636ddf-8c29-444b-91f7-a9a403e30a0c");
    public static readonly Guid Argon2idUuid = new("9e298b19-6db4-4830-bda5-57f0f7ca20c7");
    public static readonly Guid KeePassArgon2idUuid = new("9e298b19-56db-4773-b23d-fc3ec6f0a1e6");

    public byte[] Salt { get; } = salt;
    public int Parallelism { get; } = parallelism;
    public int MemoryKib { get; } = memoryKib;
    public int Iterations { get; } = iterations;
    public Argon2Type Type { get; } = type;

    public byte[] Transform(byte[] rawKey)
    {
        int bcType = Type == Argon2Type.Id ? Argon2Parameters.Argon2id : Argon2Parameters.Argon2d;

        Argon2Parameters parameters = new Argon2Parameters.Builder(bcType)
            .WithSalt(Salt)
            .WithParallelism(Parallelism)
            .WithMemoryAsKB(MemoryKib)
            .WithIterations(Iterations)
            .WithVersion(Argon2Parameters.Version13)
            .Build();

        var gen = new Argon2BytesGenerator();
        gen.Init(parameters);

        byte[] result = new byte[32];
        gen.GenerateBytes(rawKey, result);
        return result;
    }

    public VariantMap Parameters() =>
        new(
            new Dictionary<string, object>
            {
                ["$UUID"] = GuidRfc4122.ToBytes(Type == Argon2Type.Id ? Argon2idUuid : Argon2dUuid),
                ["S"] = Salt,
                ["P"] = (uint)Parallelism,
                ["M"] = (ulong)(MemoryKib * 1024L), // stored as bytes in KDBX
                ["I"] = (ulong)Iterations,
                ["V"] = (uint)0x13,
            }
        );
}
