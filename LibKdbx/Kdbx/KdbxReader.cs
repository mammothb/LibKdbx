using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;

namespace LibKdbx;

/// <summary>
/// Reads KDBX 3.x and 4.x binary files. Handles HMAC-SHA256 blocks (V4),
/// hashed blocks (V3), decompression, decryption, and delegates XML parsing
/// to <see cref="KdbxXmlReader"/>.
/// </summary>
public class KdbxReader(Database db)
{
    private readonly Database _db = db;

    public void ReadFrom(Stream stream)
    {
        var recording = new RecordingStream(stream);
        var header = KdbxHeader.Read(new BinaryReader(recording));
        byte[] headerBytes = recording.GetRecordedBytes();

        _db.Version = header.Version;

        IKdf kdf = header.CreateKdf();
        var derived = DerivedKey.Derive(_db.Key, kdf);
        var encKey = new EncryptionKey(header.MasterSeed, derived);

        if (header.IsVersion4)
        {
            ReadV4(header, encKey, headerBytes, stream);
        }
        else
        {
            ReadV3(header, encKey, stream);
        }
    }

    // ── KDBX 3.x ─────────────────────────────────────────────────────────

    private void ReadV3(KdbxHeader header, EncryptionKey encKey, Stream stream)
    {
        SymmetricCipher cipher = header.CreateCipher(encKey.GetKey());
        using Stream decrypted = cipher.CreateDecryptingStream(stream);
        var reader = new BinaryReader(decrypted);

        byte[] startBytes = reader.ReadBytes(32);
        if (!startBytes.AsSpan().SequenceEqual(header.StreamStartBytes!))
        {
            throw new InvalidDataException("StreamStartBytes verification failed.");
        }

        byte[] plaintext = ReadHashedBlocks(reader);
        if (header.IsCompressed)
        {
            plaintext = Decompress(plaintext);
        }

        var ps = new ProtectedStream(header.InnerRandomStreamId, header.ProtectedStreamKey!);
        _db.Settings = Settings.FromHeader(header, header.InnerRandomStreamId);

        using var xmlStream = new MemoryStream(plaintext);
        new KdbxXmlReader(_db, ps, isV4: false).ReadFrom(xmlStream);
    }

    // ── KDBX 4.x ─────────────────────────────────────────────────────────

    private void ReadV4(KdbxHeader header, EncryptionKey encKey, byte[] headerBytes, Stream stream)
    {
        var fileReader = new BinaryReader(stream);

        byte[] sha = fileReader.ReadBytes(32);
        if (!sha.AsSpan().SequenceEqual(SHA256.HashData(headerBytes)))
        {
            throw new InvalidDataException("Header SHA256 verification failed.");
        }

        byte[] headerHmac = fileReader.ReadBytes(32);
        byte[] headerHmacKey = BlockKey(ulong.MaxValue, encKey.GetHmacKey());
        using (var hmac = new HMACSHA256(headerHmacKey))
        {
            if (!headerHmac.AsSpan().SequenceEqual(hmac.ComputeHash(headerBytes)))
            {
                throw new InvalidDataException("Header HMAC verification failed.");
            }
        }

        byte[] ciphertext = ReadHmacBlocks(fileReader, encKey.GetHmacKey());

        SymmetricCipher cipher = header.CreateCipher(encKey.GetKey());
        using Stream decryptedStream = cipher.CreateDecryptingStream(new MemoryStream(ciphertext));

        using var plainStream = new MemoryStream();
        if (header.IsCompressed)
        {
            using var gzip = new GZipStream(decryptedStream, CompressionMode.Decompress);
            gzip.CopyTo(plainStream);
        }
        else
        {
            decryptedStream.CopyTo(plainStream);
        }
        plainStream.Position = 0;

        var innerReader = new BinaryReader(plainStream);
        (ProtectedStreamAlgorithm algo, byte[] innerKey, List<BinaryPoolEntry> binaries) =
            ReadInnerHeader(innerReader);

        var ps = new ProtectedStream(algo, innerKey);
        _db.Settings = Settings.FromHeader(header, algo);

        new KdbxXmlReader(_db, ps, isV4: true, binaries).ReadFrom(plainStream);
    }

    // ── Hashed blocks (KDBX 3.x) ─────────────────────────────────────────

    private static byte[] ReadHashedBlocks(BinaryReader reader)
    {
        using var result = new MemoryStream();
        while (true)
        {
            reader.ReadUInt32(); // block index
            byte[] hash = reader.ReadBytes(32);
            int size = (int)reader.ReadUInt32();
            byte[] data = reader.ReadBytes(size);

            if (size == 0)
            {
                break;
            }

            if (!SHA256.HashData(data).AsSpan().SequenceEqual(hash))
            {
                throw new InvalidDataException("Block hash verification failed.");
            }

            result.Write(data);
        }
        return result.ToArray();
    }

    // ── HMAC blocks (KDBX 4.x) ───────────────────────────────────────────

    private static byte[] ReadHmacBlocks(BinaryReader reader, byte[] hmacKey64)
    {
        using var result = new MemoryStream();
        ulong blockIndex = 0;

        while (true)
        {
            byte[] hmac = reader.ReadBytes(32);
            int size = reader.ReadInt32();
            byte[] data = reader.ReadBytes(size);

            byte[] blockKey = BlockKey(blockIndex, hmacKey64);
            byte[] expectedHmac = BlockHmac(blockIndex, size, data, blockKey);
            if (!hmac.AsSpan().SequenceEqual(expectedHmac))
            {
                throw new InvalidDataException($"Block {blockIndex} HMAC verification failed.");
            }

            if (size == 0)
            {
                break;
            }
            result.Write(data);
            blockIndex++;
        }
        return result.ToArray();
    }

    private static byte[] BlockKey(ulong blockIndex, byte[] hmacKey64)
    {
        byte[] buf = new byte[8 + hmacKey64.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(buf, blockIndex);
        hmacKey64.CopyTo(buf, 8);
        return SHA512.HashData(buf);
    }

    private static byte[] BlockHmac(ulong blockIndex, int blockSize, byte[] data, byte[] blockKey)
    {
        byte[] msg = new byte[8 + 4 + data.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(msg, blockIndex);
        BinaryPrimitives.WriteInt32LittleEndian(msg.AsSpan(8), blockSize);
        data.CopyTo(msg, 12);
        using var hmac = new HMACSHA256(blockKey);
        return hmac.ComputeHash(msg);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static (
        ProtectedStreamAlgorithm algo,
        byte[] key,
        List<BinaryPoolEntry> binaries
    ) ReadInnerHeader(BinaryReader reader)
    {
        ProtectedStreamAlgorithm algo = ProtectedStreamAlgorithm.ChaCha20;
        byte[]? key = null;
        var binaries = new List<BinaryPoolEntry>();

        while (true)
        {
            byte id = reader.ReadByte();
            int len = (int)reader.ReadUInt32();
            byte[] data = reader.ReadBytes(len);

            if (id == 0x00)
            {
                break;
            }
            switch (id)
            {
                case 0x01:
                    algo = (ProtectedStreamAlgorithm)BinaryPrimitives.ReadUInt32LittleEndian(data);
                    break;
                case 0x02:
                    key = data;
                    break;
                case 0x03:
                    binaries.Add(new BinaryPoolEntry((data[0] & 0x01) != 0, data[1..]));
                    break;
            }
        }

        return (algo, key ?? [], binaries);
    }

    // ── RecordingStream ──────────────────────────────────────────────────

    private sealed class RecordingStream(Stream inner) : Stream
    {
        private readonly Stream _inner = inner;
        private readonly MemoryStream _buffer = new();

        public byte[] GetRecordedBytes() => _buffer.ToArray();

        public override bool CanRead => _inner.CanRead;
        public override bool CanWrite => false;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = _inner.Read(buffer, offset, count);
            _buffer.Write(buffer, offset, n);
            return n;
        }

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override void Flush() => _inner.Flush();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _buffer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
