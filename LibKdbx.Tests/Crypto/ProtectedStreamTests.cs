using System.Security.Cryptography;

namespace LibKdbx.Tests;

public class ProtectedStreamTests
{
    [Fact]
    public void ChaCha20_RoundTrip()
    {
        byte[] key = new byte[32];
        RandomNumberGenerator.Fill(key);
        byte[] plaintext = "Protected field value — secret data."u8.ToArray();

        ProtectedStream stream = new(ProtectedStreamAlgorithm.ChaCha20, key);

        byte[] ciphertext = stream.Process(plaintext);
        ciphertext.ShouldNotBe(plaintext);

        // Re-initialize with same key to decrypt
        ProtectedStream decrypt = new(ProtectedStreamAlgorithm.ChaCha20, key);
        byte[] decrypted = decrypt.Process(ciphertext);
        decrypted.ShouldBe(plaintext);
    }

    [Fact]
    public void Salsa20_RoundTrip()
    {
        byte[] key = new byte[32];
        RandomNumberGenerator.Fill(key);
        byte[] plaintext = "Another protected field."u8.ToArray();

        ProtectedStream stream = new(ProtectedStreamAlgorithm.Salsa20, key);

        byte[] ciphertext = stream.Process(plaintext);
        ciphertext.ShouldNotBe(plaintext);

        ProtectedStream decrypt = new(ProtectedStreamAlgorithm.Salsa20, key);
        byte[] decrypted = decrypt.Process(ciphertext);
        decrypted.ShouldBe(plaintext);
    }

    [Fact]
    public void Keystream_Advances_Consistently()
    {
        byte[] key = new byte[32];
        RandomNumberGenerator.Fill(key);

        byte[] part1 = "AAA"u8.ToArray();
        byte[] part2 = "BBB"u8.ToArray();
        byte[] combined = "AAABBB"u8.ToArray();

        // Process in two chunks
        ProtectedStream stream1 = new(ProtectedStreamAlgorithm.ChaCha20, key);
        byte[] enc1 = stream1.Process(part1);
        byte[] enc2 = stream1.Process(part2);

        // Process as one chunk
        ProtectedStream stream2 = new(ProtectedStreamAlgorithm.ChaCha20, key);
        byte[] enc3 = stream2.Process(combined);

        // The concatenated two-chunk result must equal the one-chunk result
        enc1.Concat(enc2).ShouldBe(enc3);
    }

    [Fact]
    public void DifferentKeys_Produce_Different_Output()
    {
        byte[] key1 = new byte[32];
        byte[] key2 = new byte[32];
        RandomNumberGenerator.Fill(key1);
        RandomNumberGenerator.Fill(key2);
        byte[] plaintext = "test"u8.ToArray();

        ProtectedStream stream1 = new(ProtectedStreamAlgorithm.ChaCha20, key1);
        ProtectedStream stream2 = new(ProtectedStreamAlgorithm.ChaCha20, key2);

        stream1.Process(plaintext).ShouldNotBe(stream2.Process(plaintext));
    }

    [Fact]
    public void Constructor_UnknownAlgorithm_Throws()
    {
        byte[] key = new byte[32];
        // Cast an invalid integer to the enum to hit the default throw
        ProtectedStreamAlgorithm bad = (ProtectedStreamAlgorithm)99;
        Should.Throw<NotSupportedException>(() => new ProtectedStream(bad, key));
    }
}
