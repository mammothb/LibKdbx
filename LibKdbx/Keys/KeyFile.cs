using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace LibKdbx;

public enum KeyFileFormat
{
    Xml,
    Raw,
}

/// <summary>
/// Generates and reads KeePass key files in XML v1 or raw 32-byte format.
/// Key file parsing logic (XML/hex/raw/SHA-256 fallback) is embedded in <see cref="CompositeKey"/>.
/// </summary>
public static class KeyFile
{
    public static void Generate(string path, KeyFileFormat format = KeyFileFormat.Xml)
    {
        byte[] key32 = RandomNumberGenerator.GetBytes(32);
        byte[] bytes = format switch
        {
            KeyFileFormat.Xml => BuildXml(key32),
            KeyFileFormat.Raw => key32,
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        File.WriteAllBytes(path, bytes);
    }

    private static byte[] BuildXml(byte[] key32)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                "KeyFile",
                new XElement("Meta", new XElement("Version", "1.0")),
                new XElement("Key", new XElement("Data", Convert.ToBase64String(key32)))
            )
        );

        using var ms = new MemoryStream();
        using (
            var writer = new XmlTextWriter(ms, Encoding.UTF8) { Formatting = Formatting.Indented }
        )
        {
            doc.Save(writer);
        }
        return ms.ToArray();
    }
}
