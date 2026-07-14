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
            compress: false);

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
        System.Text.Encoding.UTF8.GetString(readBack.PublicCustomData)
            .ShouldContain("Name: MyDB");
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
            compress: false);

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
}
