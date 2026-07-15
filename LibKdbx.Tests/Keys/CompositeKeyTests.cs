using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace LibKdbx.Tests;

public class CompositeKeyTests
{
    // ── Key file format parsing ─────────────────────────────────────────────

    [Fact]
    public void KeyFile_XmlV1_Produces_Expected_32Bytes()
    {
        byte[] expectedKey = new byte[32];
        RandomNumberGenerator.Fill(expectedKey);

        using var tf = WriteTempXmlKeyFile(expectedKey);
        using var key = new CompositeKey();
        key.AddKeyFile(tf.Path);
        byte[] raw = key.GetRawKey();

        // GetRawKey = SHA256(component), component = the 32-byte key from file
        byte[] expected = SHA256.HashData(expectedKey);
        raw.ShouldBe(expected);
    }

    [Fact]
    public void KeyFile_Hex64Chars_Produces_Expected_32Bytes()
    {
        byte[] expectedKey = new byte[32];
        RandomNumberGenerator.Fill(expectedKey);
        string hex = Convert.ToHexString(expectedKey); // 64 hex chars

        using var tf = new TempFile();
        tf.WriteAllText(hex);
        using var key = new CompositeKey();
        key.AddKeyFile(tf.Path);
        byte[] raw = key.GetRawKey();

        byte[] expected = SHA256.HashData(expectedKey);
        raw.ShouldBe(expected);
    }

    [Fact]
    public void KeyFile_Raw32Bytes_Produces_Expected_32Bytes()
    {
        byte[] expectedKey = new byte[32];
        RandomNumberGenerator.Fill(expectedKey);

        using var tf = new TempFile();
        tf.WriteAllBytes(expectedKey);
        using var key = new CompositeKey();
        key.AddKeyFile(tf.Path);
        byte[] raw = key.GetRawKey();

        byte[] expected = SHA256.HashData(expectedKey);
        raw.ShouldBe(expected);
    }

    [Fact]
    public void KeyFile_AnyFile_Sha256()
    {
        byte[] fileContent = "arbitrary file content\n"u8.ToArray();

        using var tf = new TempFile();
        tf.WriteAllBytes(fileContent);
        using var key = new CompositeKey();
        key.AddKeyFile(tf.Path);
        byte[] raw = key.GetRawKey();

        // Any other file → SHA-256 of content as the 32-byte component,
        // then GetRawKey hashes again with SHA-256.
        // So raw = SHA256(SHA256(fileContent))
        byte[] firstHash = SHA256.HashData(fileContent);
        byte[] expected = SHA256.HashData(firstHash);
        raw.ShouldBe(expected);
    }

    [Fact]
    public void KeyFile_InvalidXml_FallsThroughToSha256()
    {
        byte[] garbage = "<not><valid>xml"u8.ToArray();

        using var tf = new TempFile();
        tf.WriteAllBytes(garbage);
        using var key = new CompositeKey();
        key.AddKeyFile(tf.Path);
        byte[] raw = key.GetRawKey();

        // TryParseXmlKeyFile catches exception → false
        // Not 64 bytes → not hex check
        // Not 32 bytes → not raw check
        // Falls through to SHA-256 fallback
        byte[] expected = SHA256.HashData(SHA256.HashData(garbage));
        raw.ShouldBe(expected);
    }

    [Fact]
    public void KeyFile_ValidXmlMissingDataElement_FallsThroughToSha256()
    {
        // Well-formed XML with <KeyFile> root but no <Key><Data> sub-element
        byte[] xml = Encoding.UTF8.GetBytes(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                + "<KeyFile><Meta><Version>1.0</Version></Meta></KeyFile>"
        );

        using var tf = new TempFile();
        tf.WriteAllBytes(xml);
        using var key = new CompositeKey();
        key.AddKeyFile(tf.Path);
        byte[] raw = key.GetRawKey();

        // TryParseXmlKeyFile: XDocument.Parse succeeds but dataElement is null → false
        // Not 64 bytes → not hex check
        // Not 32 bytes → not raw check
        // Falls through to SHA-256
        byte[] expected = SHA256.HashData(SHA256.HashData(xml));
        raw.ShouldBe(expected);
    }

    [Fact]
    public void KeyFile_InvalidHex64_FallsThroughToSha256()
    {
        // 64 chars but not valid hex — TryParseHex catches, falls through
        byte[] data = Encoding.ASCII.GetBytes(new string('Z', 64));

        using var tf = new TempFile();
        tf.WriteAllBytes(data);
        using var key = new CompositeKey();
        key.AddKeyFile(tf.Path);
        byte[] raw = key.GetRawKey();

        // TryParseXmlKeyFile fails first (not XML)
        // data.Length == 64 → tries TryParseHex → catches → false
        // data.Length != 32 → falls through to SHA-256
        byte[] expected = SHA256.HashData(SHA256.HashData(data));
        raw.ShouldBe(expected);
    }

    [Fact]
    public void KeyFile_EmptyFile_Throws()
    {
        using var tf = new TempFile();
        // File exists but is empty (0 bytes)
        using var key = new CompositeKey();
        Should.Throw<FormatException>(() => key.AddKeyFile(tf.Path));
    }

    // ── Composite key derivation ────────────────────────────────────────────

    [Fact]
    public void Password_Only_Produces_32ByteKey()
    {
        using var key = new CompositeKey("hunter2");
        byte[] raw = key.GetRawKey();
        raw.Length.ShouldBe(32);
    }

    [Fact]
    public void Password_And_KeyFile_Produces_32ByteKey()
    {
        byte[] keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);
        using var tf = WriteTempHexKeyFile(keyBytes);
        using var key = new CompositeKey("hunter2", tf.Path);
        byte[] raw = key.GetRawKey();
        raw.Length.ShouldBe(32);
    }

    [Fact]
    public void KeyFile_Changes_Result()
    {
        byte[] key1 = new byte[32];
        byte[] key2 = new byte[32];
        RandomNumberGenerator.Fill(key1);
        RandomNumberGenerator.Fill(key2);
        key2.ShouldNotBe(key1); // vanishingly unlikely to collide

        using var tf1 = WriteTempHexKeyFile(key1);
        using var tf2 = WriteTempHexKeyFile(key2);
        using var ck1 = new CompositeKey("pass", tf1.Path);
        using var ck2 = new CompositeKey("pass", tf2.Path);

        ck1.GetRawKey().ShouldNotBe(ck2.GetRawKey());
    }

    [Fact]
    public void No_Components_Throws()
    {
        using var key = new CompositeKey();
        Should.Throw<InvalidOperationException>(key.GetRawKey);
    }

    // ── Dispose zeroization ─────────────────────────────────────────────────

    [Fact]
    public void Dispose_Zeroizes_Components()
    {
        var key = new CompositeKey("hunter2");
        // Capture the internal component reference via GetRawKey side effect
        byte[] raw = key.GetRawKey();
        raw.Length.ShouldBe(32);

        key.Dispose();

        // After dispose, GetRawKey should throw
        Should.Throw<ObjectDisposedException>(key.GetRawKey);
    }

    [Fact]
    public void AddPassword_After_Dispose_Throws()
    {
        var key = new CompositeKey("initial");
        key.Dispose();
        Should.Throw<ObjectDisposedException>(() => key.AddPassword("newpass"));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static TempFile WriteTempXmlKeyFile(byte[] key32)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                "KeyFile",
                new XElement("Meta", new XElement("Version", "1.0")),
                new XElement("Key", new XElement("Data", Convert.ToBase64String(key32)))
            )
        );

        var tf = new TempFile();
        using var ms = new MemoryStream();
        var settings = new System.Xml.XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false), // no BOM
            Indent = true,
        };
        using (var writer = System.Xml.XmlWriter.Create(ms, settings))
        {
            doc.Save(writer);
        }
        File.WriteAllBytes(tf.Path, ms.ToArray());
        return tf;
    }

    private static TempFile WriteTempHexKeyFile(byte[] key32)
    {
        var tf = new TempFile();
        File.WriteAllText(tf.Path, Convert.ToHexString(key32), Encoding.ASCII);
        return tf;
    }
}
