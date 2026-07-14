namespace LibKdbx.Tests;

public class SettingsTests
{
    [Fact]
    public void NewFields_DefaultValues()
    {
        var settings = new Settings();
        settings.DatabaseUuid.ShouldNotBe(Guid.Empty);
        settings.PublicUuid.ShouldNotBe(Guid.Empty);
        settings.PublicName.ShouldBe("");
        settings.PublicColor.ShouldBe("");
        settings.PublicIcon.ShouldBe(0);
    }

    [Fact]
    public void PublicCustomData_RoundTrip_Through_Header()
    {
        var settings = new Settings
        {
            PublicName = "MyDB",
            PublicColor = "#FF0000",
            PublicIcon = 42,
        };

        KdbxHeader header = settings.ToHeader();

        // Read back through FromHeader
        var roundTripped = Settings.FromHeader(header, ProtectedStreamAlgorithm.ChaCha20);
        roundTripped.PublicName.ShouldBe("MyDB");
        roundTripped.PublicColor.ShouldBe("#FF0000");
        roundTripped.PublicIcon.ShouldBe(42);
    }

    [Fact]
    public void PublicUuid_RoundTrip_Through_Header()
    {
        Guid uuid = Guid.NewGuid();
        var settings = new Settings { PublicUuid = uuid };

        KdbxHeader header = settings.ToHeader();
        var roundTripped = Settings.FromHeader(header, ProtectedStreamAlgorithm.ChaCha20);

        roundTripped.PublicUuid.ShouldBe(uuid);
    }

    [Fact]
    public void PublicCustomData_Empty_When_All_Defaults()
    {
        var settings = new Settings
        {
            PublicUuid = Guid.Empty,
            PublicName = "",
            PublicColor = "",
            PublicIcon = 0,
        };

        KdbxHeader header = settings.ToHeader();
        // Empty public custom data should not be written
        header.PublicCustomData.ShouldBeNull();
    }

    [Fact]
    public void ToHeader_V3_Throws_If_Not_AesKdf()
    {
        var settings = new Settings { Format = KdbxFormat.Kdbx3 };
        // Kdf defaults to Argon2
        Should.Throw<InvalidOperationException>(settings.ToHeader);
    }

    [Fact]
    public void ToHeader_V3_With_AesKdf_Succeeds()
    {
        var settings = new Settings
        {
            Format = KdbxFormat.Kdbx3,
            Kdf = new AesKdf(new byte[16], 100_000),
        };

        KdbxHeader header = settings.ToHeader();
        header.IsVersion4.ShouldBeFalse();
    }

    [Fact]
    public void FromHeader_V3_Reads_Format()
    {
        var header = KdbxHeader.CreateNewV3(
            CipherAlgorithm.Aes256Cbc,
            ProtectedStreamAlgorithm.Salsa20,
            rounds: 100_000,
            compress: false
        );

        var settings = Settings.FromHeader(header, ProtectedStreamAlgorithm.Salsa20);
        settings.Format.ShouldBe(KdbxFormat.Kdbx3);
        settings.Cipher.ShouldBe(CipherAlgorithm.Aes256Cbc);
        settings.IsCompressed.ShouldBeFalse();
    }

    [Fact]
    public void FromHeader_V4_Reads_Format()
    {
        var header = KdbxHeader.CreateNewV4(CipherAlgorithm.ChaCha20, kdf: null, compress: true);
        var settings = Settings.FromHeader(header, ProtectedStreamAlgorithm.ChaCha20);
        settings.Format.ShouldBe(KdbxFormat.Kdbx4);
        settings.Cipher.ShouldBe(CipherAlgorithm.ChaCha20);
        settings.IsCompressed.ShouldBeTrue();
    }
}
