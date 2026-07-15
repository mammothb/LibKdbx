using System.Security.Cryptography;

namespace LibKdbx.Tests;

/// <summary>
/// Crypto test helpers.
/// </summary>
public static class CryptoHelpers
{
    /// <summary>Returns a new byte array filled with cryptographically random bytes.</summary>
    public static byte[] GetRandomBytes(int size)
    {
        byte[] data = new byte[size];
        RandomNumberGenerator.Fill(data);
        return data;
    }
}
