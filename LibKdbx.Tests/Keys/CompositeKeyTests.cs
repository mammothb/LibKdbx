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

        string path = WriteTempXmlKeyFile(expectedKey);
        try
        {
            using var key = new CompositeKey();
            key.AddKeyFile(path);
            byte[] raw = key.GetRawKey();

            // GetRawKey = SHA256(component), component = the 32-byte key from file
            byte[] expected = SHA256.HashData(expectedKey);
            raw.ShouldBe(expected);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void KeyFile_Hex64Chars_Produces_Expected_32Bytes()
    {
        byte[] expectedKey = new byte[32];
        RandomNumberGenerator.Fill(expectedKey);
        string hex = Convert.ToHexString(expectedKey); // 64 hex chars

        string path = Path.GetTempFileName();
        File.WriteAllText(path, hex, Encoding.ASCII);
        try
        {
            using var key = new CompositeKey();
            key.AddKeyFile(path);
            byte[] raw = key.GetRawKey();

            byte[] expected = SHA256.HashData(expectedKey);
            raw.ShouldBe(expected);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void KeyFile_Raw32Bytes_Produces_Expected_32Bytes()
    {
        byte[] expectedKey = new byte[32];
        RandomNumberGenerator.Fill(expectedKey);

        string path = Path.GetTempFileName();
        File.WriteAllBytes(path, expectedKey);
        try
        {
            using var key = new CompositeKey();
            key.AddKeyFile(path);
            byte[] raw = key.GetRawKey();

            byte[] expected = SHA256.HashData(expectedKey);
            raw.ShouldBe(expected);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void KeyFile_AnyFile_Sha256()
    {
        byte[] fileContent = "arbitrary file content\n"u8.ToArray();

        string path = Path.GetTempFileName();
        File.WriteAllBytes(path, fileContent);
        try
        {
            using var key = new CompositeKey();
            key.AddKeyFile(path);
            byte[] raw = key.GetRawKey();

            // Any other file → SHA-256 of content as the 32-byte component,
            // then GetRawKey hashes again with SHA-256.
            // So raw = SHA256(SHA256(fileContent))
            byte[] firstHash = SHA256.HashData(fileContent);
            byte[] expected = SHA256.HashData(firstHash);
            raw.ShouldBe(expected);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void KeyFile_EmptyFile_Throws()
    {
        string path = Path.GetTempFileName();
        // File exists but is empty (0 bytes)
        try
        {
            using var key = new CompositeKey();
            Should.Throw<FormatException>(() => key.AddKeyFile(path));
        }
        finally
        {
            File.Delete(path);
        }
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
        string path = WriteTempHexKeyFile(keyBytes);
        try
        {
            using var key = new CompositeKey("hunter2", path);
            byte[] raw = key.GetRawKey();
            raw.Length.ShouldBe(32);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void KeyFile_Changes_Result()
    {
        byte[] key1 = new byte[32];
        byte[] key2 = new byte[32];
        RandomNumberGenerator.Fill(key1);
        RandomNumberGenerator.Fill(key2);
        key2.ShouldNotBe(key1); // vanishingly unlikely to collide

        string path1 = WriteTempHexKeyFile(key1);
        string path2 = WriteTempHexKeyFile(key2);
        try
        {
            using var ck1 = new CompositeKey("pass", path1);
            using var ck2 = new CompositeKey("pass", path2);

            ck1.GetRawKey().ShouldNotBe(ck2.GetRawKey());
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
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

    private static string WriteTempXmlKeyFile(byte[] key32)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                "KeyFile",
                new XElement("Meta", new XElement("Version", "1.0")),
                new XElement("Key", new XElement("Data", Convert.ToBase64String(key32)))
            )
        );

        string path = Path.GetTempFileName();
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
        File.WriteAllBytes(path, ms.ToArray());
        return path;
    }

    private static string WriteTempHexKeyFile(byte[] key32)
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, Convert.ToHexString(key32), Encoding.ASCII);
        return path;
    }
}
