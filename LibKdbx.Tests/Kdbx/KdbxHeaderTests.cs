namespace LibKdbx.Tests;

public class KdbxHeaderTests
{
    [Fact]
    public void V4_CreateRead_RoundTrip()
    {
        var header = KdbxHeader.CreateNewV4(CipherAlgorithm.ChaCha20, kdf: null, compress: true);

        using var ms = new MemoryStream();
        header.Write(ms);
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        var readBack = KdbxHeader.Read(reader);

        readBack.IsVersion4.ShouldBeTrue();
        readBack.CipherId.ShouldBe(header.CipherId);
        readBack.IsCompressed.ShouldBe(header.IsCompressed);
        readBack.MasterSeed.ShouldBe(header.MasterSeed);
        readBack.EncryptionIV.ShouldBe(header.EncryptionIV);
    }

    [Fact]
    public void V3_CreateRead_RoundTrip()
    {
        var header = KdbxHeader.CreateNewV3(
            CipherAlgorithm.Aes256Cbc,
            ProtectedStreamAlgorithm.Salsa20,
            rounds: 100_000,
            compress: false
        );

        using var ms = new MemoryStream();
        header.Write(ms);
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        var readBack = KdbxHeader.Read(reader);

        readBack.IsVersion4.ShouldBeFalse();
        readBack.CipherId.ShouldBe(SymmetricCipher.Aes256Uuid);
        readBack.IsCompressed.ShouldBeFalse();
        readBack.MasterSeed.ShouldBe(header.MasterSeed);
        readBack.TransformSeed.ShouldBe(header.TransformSeed);
        readBack.TransformRounds.ShouldBe(100_000UL);
        readBack.InnerStreamAlgorithm.ShouldBe(ProtectedStreamAlgorithm.Salsa20);
    }

    [Fact]
    public void V4_PublicCustomData_RoundTrip()
    {
        var header = KdbxHeader.CreateNewV4(CipherAlgorithm.ChaCha20, kdf: null, compress: true);
        header.PublicCustomData = "Name: MyDB\nColor: #FF0000\n"u8.ToArray();

        using var ms = new MemoryStream();
        header.Write(ms);
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        var readBack = KdbxHeader.Read(reader);

        readBack.PublicCustomData.ShouldNotBeNull();
        System.Text.Encoding.UTF8.GetString(readBack.PublicCustomData).ShouldContain("Name: MyDB");
    }

    [Fact]
    public void CreateKdf_V4_Default_Returns_Argon2id()
    {
        var header = KdbxHeader.CreateNewV4(CipherAlgorithm.ChaCha20, kdf: null, compress: true);
        IKdf kdf = header.CreateKdf();
        kdf.ShouldBeOfType<Argon2Kdf>();
        ((Argon2Kdf)kdf).Type.ShouldBe(Argon2Type.Id);
    }

    [Fact]
    public void CreateKdf_V3_Returns_AesKdf()
    {
        var header = KdbxHeader.CreateNewV3(
            CipherAlgorithm.Aes256Cbc,
            ProtectedStreamAlgorithm.Salsa20,
            rounds: 100_000,
            compress: false
        );

        IKdf kdf = header.CreateKdf();
        kdf.ShouldBeOfType<AesKdf>();
    }

    [Fact]
    public void Invalid_Signature_Throws()
    {
        var header = KdbxHeader.CreateNewV4(CipherAlgorithm.ChaCha20, kdf: null, compress: true);

        using var ms = new MemoryStream();
        // Write a bad signature
        var badSig = new Signature(0xDEADBEEF, 0xCAFEBABE);
        badSig.Write(ms);
        // Write the rest of the header
        var restMs = new MemoryStream();
        header.Write(restMs);
        restMs.Position = 0;
        restMs.CopyTo(ms);
        ms.Position = 0;

        using var reader = new BinaryReader(ms);
        Should.Throw<FormatException>(() => KdbxHeader.Read(reader));
    }

    // ── CreateNew V3 path ───────────────────────────────────────────────

    [Fact]
    public void CreateNew_V3_Produces_V3_Header()
    {
        var header = KdbxHeader.CreateNew(
            v4: false,
            cipher: CipherAlgorithm.Aes256Cbc,
            compress: false
        );
        header.IsVersion4.ShouldBeFalse();
        header.CipherId.ShouldBe(SymmetricCipher.Aes256Uuid);
        header.TransformSeed.ShouldNotBeNull();
        header.StreamStartBytes.ShouldNotBeNull();
    }

    // ── CreateKdf variants ──────────────────────────────────────────────

    [Fact]
    public void CreateKdf_Argon2d_From_Parameters()
    {
        var kdf = new Argon2Kdf(new byte[32], 2, 16 * 1024, 2, Argon2Type.D);
        var header = KdbxHeader.CreateNewV4(CipherAlgorithm.ChaCha20, kdf, compress: true);
        IKdf result = header.CreateKdf();
        result.ShouldBeOfType<Argon2Kdf>();
        ((Argon2Kdf)result).Type.ShouldBe(Argon2Type.D);
    }

    [Fact]
    public void CreateKdf_AesKdf_From_V4_Parameters()
    {
        // Build header with AES-KDF in KdfParameters via $UUID
        byte[] seed = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(seed);
        var kdf = new AesKdf(seed, 100_000);
        var header = KdbxHeader.CreateNewV4(CipherAlgorithm.ChaCha20, kdf, compress: true);

        IKdf result = header.CreateKdf();
        result.ShouldBeOfType<AesKdf>();
    }

    [Fact]
    public void CreateKdf_Null_KdfParameters_Throws()
    {
        var header = KdbxHeader.CreateNewV4(CipherAlgorithm.ChaCha20, kdf: null, compress: true);
        typeof(KdbxHeader).GetProperty("KdfParameters")!.SetValue(header, null);
        Should.Throw<InvalidOperationException>(header.CreateKdf);
    }

    [Fact]
    public void CreateKdf_V3_Null_TransformSeed_Throws()
    {
        // Create a V3 header without TransformSeed
        var header = KdbxHeader.CreateNewV3(
            CipherAlgorithm.Aes256Cbc,
            ProtectedStreamAlgorithm.Salsa20,
            100_000,
            false
        );
        typeof(KdbxHeader).GetProperty("TransformSeed")!.SetValue(header, null);
        Should.Throw<InvalidOperationException>(header.CreateKdf);
    }

    // ── SetInnerStream ──────────────────────────────────────────────────

    [Fact]
    public void SetInnerStream_Stores_Key()
    {
        var header = KdbxHeader.CreateNewV3(
            CipherAlgorithm.Aes256Cbc,
            ProtectedStreamAlgorithm.Salsa20,
            100_000,
            false
        );
        byte[] key = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(key);
        header.SetInnerStream(ProtectedStreamAlgorithm.Salsa20, key);
        // For V3, InnerStreamAlgorithm reflects the set value
        header.InnerStreamAlgorithm.ShouldBe(ProtectedStreamAlgorithm.Salsa20);
    }

    // ── V4 InnerStreamAlgorithm is always ChaCha20 ──────────────────────

    [Fact]
    public void InnerStreamAlgorithm_V4_Always_ChaCha20()
    {
        var header = KdbxHeader.CreateNewV4(CipherAlgorithm.ChaCha20, kdf: null, compress: true);
        header.InnerStreamAlgorithm.ShouldBe(ProtectedStreamAlgorithm.ChaCha20);
    }

    // ── Write overloads ─────────────────────────────────────────────────

    [Fact]
    public void Write_Stream_Overload()
    {
        var header = KdbxHeader.CreateNewV4(CipherAlgorithm.ChaCha20, kdf: null, compress: true);
        using var ms = new MemoryStream();
        header.Write(ms);
        ms.Position.ShouldBeGreaterThan(0);
    }

    // ── Dump ────────────────────────────────────────────────────────────

    [Fact]
    public void Dump_V4_Contains_CipherId()
    {
        var header = KdbxHeader.CreateNewV4(CipherAlgorithm.ChaCha20, kdf: null, compress: true);
        string dump = header.Dump();
        dump.ShouldContain("KDBX 4.x");
        dump.ShouldContain("CipherId");
    }

    [Fact]
    public void Dump_V3_Contains_TransformSeed()
    {
        var header = KdbxHeader.CreateNewV3(
            CipherAlgorithm.Aes256Cbc,
            ProtectedStreamAlgorithm.Salsa20,
            100_000,
            false
        );
        string dump = header.Dump();
        dump.ShouldContain("KDBX 3.x");
        dump.ShouldContain("TransformSeed");
    }
}
