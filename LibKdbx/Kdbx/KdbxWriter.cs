using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace LibKdbx;

/// <summary>
/// Writes KDBX 3.x and 4.x binary files. Handles crypto, compression,
/// HMAC-SHA256 blocks (V4), hashed blocks (V3), and delegates XML serialization
/// to <see cref="KdbxXmlWriter"/>.
/// </summary>
public class KdbxWriter(Database db)
{
    private const int BlockSize = 1024 * 1024; // 1 MiB

    private readonly Database _db = db;

    public void WriteTo(Stream stream)
    {
        Settings settings =
            _db.Settings ?? throw new InvalidOperationException("Database has no Settings.");
        KdbxHeader header = settings.ToHeader();
        byte[] psKey = RandomNumberGenerator.GetBytes(64);
        var ps = new ProtectedStream(settings.InnerStreamAlgorithm, psKey);

        IKdf kdf = header.CreateKdf();
        var derived = DerivedKey.Derive(_db.Key, kdf);
        var encKey = new EncryptionKey(header.MasterSeed, derived);

        if (header.IsVersion4)
        {
            WriteV4(stream, header, encKey, ps);
        }
        else
        {
            WriteV3(stream, header, encKey, ps);
        }
    }

    // ── KDBX 3.x ─────────────────────────────────────────────────────────

#pragma warning disable CA1859
    private void WriteV3(Stream stream, IHeader header, EncryptionKey encKey, ProtectedStream ps)
    {
        header.SetInnerStream(ps.Algorithm, ps.Key);

        var headerWriter = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        header.Write(headerWriter);
        headerWriter.Flush();

        using var xmlMs = new MemoryStream();
        new KdbxXmlWriter(_db, ps, isV4: false).WriteTo(xmlMs);
        byte[] xml = xmlMs.ToArray();
        if (header.IsCompressed)
        {
            xml = CompressGzip(xml);
        }

        using var plainMem = new MemoryStream();
        plainMem.Write(header.StreamStartBytes);
        WriteHashedBlocks(plainMem, xml);
        byte[] plaintext = plainMem.ToArray();

        SymmetricCipher cipher = header.CreateCipher(encKey.GetKey());
        using var cipherMem = new MemoryStream();
        using (Stream encStream = cipher.CreateEncryptingStream(cipherMem))
        {
            encStream.Write(plaintext);
        }
        stream.Write(cipherMem.ToArray());
    }
#pragma warning restore CA1859

    // ── KDBX 4.x ─────────────────────────────────────────────────────────

#pragma warning disable CA1859
    private void WriteV4(Stream stream, IHeader header, EncryptionKey encKey, ProtectedStream ps)
    {
        using var headerMs = new MemoryStream();
        var headerWriter = new BinaryWriter(headerMs, Encoding.UTF8, leaveOpen: true);
        header.Write(headerWriter);
        headerWriter.Flush();
        byte[] headerBytes = headerMs.ToArray();

        stream.Write(headerBytes);
        stream.Write(SHA256.HashData(headerBytes));

        byte[] headerHmacKey = BlockKey(ulong.MaxValue, encKey.GetHmacKey());
        using (var hmac = new HMACSHA256(headerHmacKey))
        {
            stream.Write(hmac.ComputeHash(headerBytes));
        }

        var xmlWriter = new KdbxXmlWriter(_db, ps, isV4: true);

        using var payloadMs = new MemoryStream();
        WriteInnerHeader(payloadMs, ps.Algorithm, ps.Key, xmlWriter.BinaryPool);
        xmlWriter.WriteTo(payloadMs);
        byte[] payload = payloadMs.ToArray();

        if (header.IsCompressed)
        {
            payload = CompressGzip(payload);
        }

        SymmetricCipher cipher = header.CreateCipher(encKey.GetKey());
        using var cipherMs = new MemoryStream();
        using (Stream encStream = cipher.CreateEncryptingStream(cipherMs))
        {
            encStream.Write(payload);
        }

        WriteHmacBlocks(stream, cipherMs.ToArray(), encKey.GetHmacKey());
    }
#pragma warning restore CA1859

    // ── Hashed blocks (KDBX 3.x) ─────────────────────────────────────────

    private static void WriteHashedBlocks(Stream output, byte[] data)
    {
        var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        int blockIndex = 0;
        int offset = 0;

        while (offset < data.Length)
        {
            int size = Math.Min(BlockSize, data.Length - offset);
            byte[] block = data[offset..(offset + size)];

            writer.Write((uint)blockIndex);
            writer.Write(SHA256.HashData(block));
            writer.Write((uint)size);
            writer.Write(block);

            offset += size;
            blockIndex++;
        }

        writer.Write((uint)blockIndex);
        writer.Write(new byte[32]); // zero hash
        writer.Write((uint)0); // size = 0
        writer.Flush();
    }

    // ── HMAC blocks (KDBX 4.x) ───────────────────────────────────────────

    private static void WriteHmacBlocks(Stream output, byte[] data, byte[] hmacKey64)
    {
        var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        ulong blockIndex = 0;
        int offset = 0;

        while (offset < data.Length)
        {
            int size = Math.Min(BlockSize, data.Length - offset);
            byte[] block = data[offset..(offset + size)];
            byte[] blockKey = BlockKey(blockIndex, hmacKey64);
            byte[] hmac = BlockHmac(blockIndex, size, block, blockKey);

            writer.Write(hmac);
            writer.Write(size);
            writer.Write(block);

            offset += size;
            blockIndex++;
        }

        byte[] termKey = BlockKey(blockIndex, hmacKey64);
        byte[] termHmac = BlockHmac(blockIndex, 0, [], termKey);
        writer.Write(termHmac);
        writer.Write(0);
        writer.Flush();
    }

    // ── Inner header (KDBX 4.x) ──────────────────────────────────────────

    private static void WriteInnerHeader(
        Stream output,
        ProtectedStreamAlgorithm algo,
        byte[] key,
        IReadOnlyList<BinaryPoolEntry> binaries
    )
    {
        var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);

        byte[] algoBytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(algoBytes, (uint)algo);
        writer.Write((byte)0x01);
        writer.Write((uint)algoBytes.Length);
        writer.Write(algoBytes);

        writer.Write((byte)0x02);
        writer.Write((uint)key.Length);
        writer.Write(key);

        foreach ((bool isProtected, byte[] data) in binaries)
        {
            byte[] payload = new byte[1 + data.Length];
            payload[0] = isProtected ? (byte)0x01 : (byte)0x00;
            data.CopyTo(payload, 1);
            writer.Write((byte)0x03);
            writer.Write((uint)payload.Length);
            writer.Write(payload);
        }

        writer.Write((byte)0x00);
        writer.Write((uint)0);
        writer.Flush();
    }

    // ── HMAC helpers ─────────────────────────────────────────────────────

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

    // ── Utilities ────────────────────────────────────────────────────────

    private static byte[] CompressGzip(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            gzip.Write(data);
        }
        return output.ToArray();
    }
}
