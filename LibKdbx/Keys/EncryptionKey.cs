using System.Security.Cryptography;

namespace LibKdbx;

/// <summary>
/// Final encryption and HMAC keys derived from the master seed and derived key.
/// Mirrors KeePassXC's key finalization step.
/// </summary>
public class EncryptionKey
{
    private readonly byte[] _key;
    private readonly byte[] _hmacKey;

    public EncryptionKey(byte[] masterSeed, DerivedKey derivedKey)
    {
        byte[] derived = derivedKey.GetRawKey();

        // SHA256(masterSeed ∥ derivedKey) — cipher key
        byte[] buf = new byte[masterSeed.Length + derived.Length];
        masterSeed.CopyTo(buf, 0);
        derived.CopyTo(buf, masterSeed.Length);
        _key = SHA256.HashData(buf);

        // SHA512(masterSeed ∥ derivedKey ∥ 0x01) — KDBX 4 HMAC key
        byte[] bufHmac = new byte[masterSeed.Length + derived.Length + 1];
        masterSeed.CopyTo(bufHmac, 0);
        derived.CopyTo(bufHmac, masterSeed.Length);
        bufHmac[^1] = 0x01;
        _hmacKey = SHA512.HashData(bufHmac);
    }

    /// <summary>32-byte key used to encrypt/decrypt the payload.</summary>
    public byte[] GetKey() => _key;

    /// <summary>64-byte key used for HMAC-SHA256 block integrity (KDBX 4 only).</summary>
    public byte[] GetHmacKey() => _hmacKey;
}
