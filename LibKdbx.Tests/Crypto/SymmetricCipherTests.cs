using System.Security.Cryptography;

namespace LibKdbx.Tests;

public class SymmetricCipherTests
{
    /// <summary>
    /// Wraps a <see cref="MemoryStream"/> so that disposing the wrapper does NOT
    /// dispose the underlying stream. Needed because the cipher streams dispose
    /// their inner target stream on close.
    /// </summary>
    private sealed class NonDisposingStream(MemoryStream inner) : Stream
    {
        private readonly MemoryStream _inner = inner;
        public override bool CanRead => _inner.CanRead;
        public override bool CanWrite => _inner.CanWrite;
        public override bool CanSeek => _inner.CanSeek;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override void Write(byte[] buffer, int offset, int count) =>
            _inner.Write(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value) => _inner.SetLength(value);
        // Intentionally does NOT forward Dispose — keeps inner MemoryStream accessible.
    }

    private static (byte[] encrypted, byte[] plaintext) Encrypt(
        CipherAlgorithm algo,
        byte[] key,
        byte[] iv
    )
    {
        byte[] plaintext = System.Text.Encoding.UTF8.GetBytes(
            $"Round-trip test for {algo} -- {Guid.NewGuid()}"
        );
        SymmetricCipher cipher = new(algo, key, iv);

        var inner = new MemoryStream();
        var wrapper = new NonDisposingStream(inner);
        using (Stream encryptStream = cipher.CreateEncryptingStream(wrapper))
        {
            encryptStream.Write(plaintext);
        }
        return (inner.ToArray(), plaintext);
    }

    private static byte[] Decrypt(CipherAlgorithm algo, byte[] key, byte[] iv, byte[] ciphertext)
    {
        SymmetricCipher cipher = new(algo, key, iv);
        using var ms = new MemoryStream(ciphertext);
        using Stream decryptStream = cipher.CreateDecryptingStream(ms);
        using var resultMs = new MemoryStream();
        decryptStream.CopyTo(resultMs);
        return resultMs.ToArray();
    }

    // ── Encryption/decryption round-trip ────────────────────────────────────

    public static IEnumerable<object[]> CipherAlgorithms()
    {
        yield return new object[] { CipherAlgorithm.Aes256Cbc, 32, 16 };
        yield return new object[] { CipherAlgorithm.ChaCha20, 32, 12 };
        yield return new object[] { CipherAlgorithm.Twofish256Cbc, 32, 16 };
        yield return new object[] { CipherAlgorithm.Aes128Cbc, 16, 16 };
    }

    [Theory]
    [MemberData(nameof(CipherAlgorithms))]
    public void EncryptDecrypt_RoundTrip(CipherAlgorithm algo, int keySize, int ivSize)
    {
        byte[] key = new byte[keySize];
        byte[] iv = new byte[ivSize];
        RandomNumberGenerator.Fill(key);
        RandomNumberGenerator.Fill(iv);

        (byte[] encrypted, byte[] plaintext) = Encrypt(algo, key, iv);
        byte[] decrypted = Decrypt(algo, key, iv, encrypted);
        decrypted.ShouldBe(plaintext);
    }

    // ── UUID mapping ────────────────────────────────────────────────────────

    [Fact]
    public void FromUuid_Returns_Correct_Algorithm()
    {
        SymmetricCipher.FromUuid(SymmetricCipher.Aes256Uuid).ShouldBe(CipherAlgorithm.Aes256Cbc);
        SymmetricCipher.FromUuid(SymmetricCipher.ChaCha20Uuid).ShouldBe(CipherAlgorithm.ChaCha20);
        SymmetricCipher
            .FromUuid(SymmetricCipher.TwofishUuid)
            .ShouldBe(CipherAlgorithm.Twofish256Cbc);
        SymmetricCipher.FromUuid(SymmetricCipher.Aes128Uuid).ShouldBe(CipherAlgorithm.Aes128Cbc);
    }

    [Fact]
    public void UuidFromAlgorithm_RoundTrip()
    {
        foreach (CipherAlgorithm algo in Enum.GetValues<CipherAlgorithm>())
        {
            Guid uuid = SymmetricCipher.UuidFromAlgorithm(algo);
            SymmetricCipher.FromUuid(uuid).ShouldBe(algo);
        }
    }

    [Fact]
    public void Unknown_Uuid_Throws()
    {
        Should.Throw<NotSupportedException>(() => SymmetricCipher.FromUuid(Guid.NewGuid()));
    }

    // ── Stream property coverage ────────────────────────────────────────

    [Fact]
    public void CipherStream_Properties_Throw_Or_Return_Correctly()
    {
        byte[] key = CryptoHelpers.GetRandomBytes(32);
        byte[] iv = CryptoHelpers.GetRandomBytes(12);

        SymmetricCipher cipher = new(CipherAlgorithm.ChaCha20, key, iv);
        using var inner = new MemoryStream();
        using Stream stream = cipher.CreateEncryptingStream(inner);

        stream.CanSeek.ShouldBeFalse();
        Should.Throw<NotSupportedException>(() =>
        {
            long _ = stream.Length;
        });
        Should.Throw<NotSupportedException>(() =>
        {
            long _ = stream.Position;
        });
        Should.Throw<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Should.Throw<NotSupportedException>(() => stream.SetLength(0));
    }

    [Fact]
    public void Twofish_Stream_Write_Then_Dispose_Flushes()
    {
        byte[] key = CryptoHelpers.GetRandomBytes(32);
        byte[] iv = CryptoHelpers.GetRandomBytes(16);

        SymmetricCipher cipher = new(CipherAlgorithm.Twofish256Cbc, key, iv);
        var inner = new MemoryStream();
        var wrapper = new NonDisposingStream(inner);
        using (Stream stream = cipher.CreateEncryptingStream(wrapper))
        {
            stream.Write("test data"u8.ToArray());
            // Flush should not throw
            stream.Flush();
        }
        // After dispose, inner stream should have encrypted data
        inner.Length.ShouldBeGreaterThan(0);
    }
}
