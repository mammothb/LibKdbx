using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace LibKdbx;

/// <summary>
/// Inner stream algorithm IDs as stored in the KDBX header (InnerRandomStreamID field).
/// </summary>
public enum ProtectedStreamAlgorithm
{
    Salsa20 = 2,
    ChaCha20 = 3,
}

/// <summary>
/// Stateful XOR stream used to encrypt/decrypt Protected="True" XML fields.
/// The keystream position advances with each call to <see cref="Process"/> — fields must be
/// processed in the same order during read and write.
/// </summary>
public class ProtectedStream(ProtectedStreamAlgorithm algorithm, byte[] key)
{
    private readonly IStreamCipher _cipher = algorithm switch
    {
        ProtectedStreamAlgorithm.Salsa20 => InitSalsa20(key),
        ProtectedStreamAlgorithm.ChaCha20 => InitChaCha20(key),
        _ => throw new NotSupportedException($"Unknown protected stream algorithm: {algorithm}"),
    };

    public ProtectedStreamAlgorithm Algorithm { get; } = algorithm;
    public byte[] Key { get; } = key;

    /// <summary>
    /// XOR data with the next keystream bytes (same operation for encrypt and decrypt).
    /// </summary>
    public byte[] Process(byte[] data)
    {
        byte[] output = new byte[data.Length];
        _cipher.ProcessBytes(data, 0, data.Length, output, 0);
        return output;
    }

#pragma warning disable CA1859
    private static IStreamCipher InitSalsa20(byte[] key)
    {
        var engine = new Salsa20Engine();
        engine.Init(
            true,
            new ParametersWithIV(
                new KeyParameter(SHA256.HashData(key)),
                [0xE8, 0x30, 0x09, 0x4B, 0x97, 0x20, 0x5D, 0x2A]
            )
        );
        return engine;
    }

    private static IStreamCipher InitChaCha20(byte[] key)
    {
        byte[] hash = SHA512.HashData(key);
        var engine = new ChaCha7539Engine();
        engine.Init(
            true,
            new ParametersWithIV(
                new KeyParameter(hash[..32]),
                hash[32..44] // 12-byte nonce
            )
        );
        return engine;
    }
#pragma warning restore CA1859
}
