using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace LibKdbx;

/// <summary>
/// Composite key = SHA256(component₁ ∥ component₂ ∥ …).
/// Each component is 32 bytes:
///   password  → SHA256(UTF8(password))
///   key file  → 32-byte key extracted from the file (XML, hex, raw, or SHA256 fallback).
/// Implements <see cref="IDisposable"/> to zeroize key material.
/// </summary>
public class CompositeKey : IDisposable
{
    private readonly List<byte[]> _components = [];
    private bool _disposed;

    public CompositeKey() { }

    public CompositeKey(string password)
    {
        AddPassword(password);
    }

    public CompositeKey(string password, string keyFile)
    {
        AddPassword(password);
        AddKeyFile(keyFile);
    }

    public CompositeKey AddPassword(string password)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _components.Add(SHA256.HashData(Encoding.UTF8.GetBytes(password ?? "")));
        return this;
    }

    public CompositeKey AddKeyFile(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _components.Add(ReadKeyFile(path));
        return this;
    }

    public byte[] GetRawKey()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_components.Count == 0)
        {
            throw new InvalidOperationException("CompositeKey has no components.");
        }

        byte[] buffer = new byte[_components.Count * 32];
        int offset = 0;
        foreach (byte[] c in _components)
        {
            c.CopyTo(buffer, offset);
            offset += 32;
        }
        return SHA256.HashData(buffer);
    }

#pragma warning disable CA1816
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        foreach (byte[] c in _components)
        {
            Array.Clear(c);
        }
        _components.Clear();
    }
#pragma warning restore CA1816

    private static byte[] ReadKeyFile(string path)
    {
        byte[] data = File.ReadAllBytes(path);

        if (data.Length == 0)
        {
            throw new FormatException("Key file is empty.");
        }

        // XML v1 key file
        if (TryParseXmlKeyFile(data, out byte[]? xmlKey))
        {
            return xmlKey!;
        }

        // 64 ASCII hex chars → 32 bytes
        if (data.Length == 64 && TryParseHex(data, out byte[]? hexKey))
        {
            return hexKey!;
        }

        // Raw 32-byte binary key
        if (data.Length == 32)
        {
            return data;
        }

        // Any other file → SHA-256 hash
        return SHA256.HashData(data);
    }

    private static bool TryParseXmlKeyFile(byte[] data, out byte[]? key)
    {
        key = null;
        try
        {
            var doc = XDocument.Parse(Encoding.UTF8.GetString(data));
            XElement? dataElement = doc.Root?.Element("Key")?.Element("Data");
            if (dataElement is null)
            {
                return false;
            }
            key = Convert.FromBase64String(dataElement.Value.Trim());
            return key.Length == 32;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseHex(byte[] data, out byte[]? key)
    {
        key = null;
        try
        {
            key = Convert.FromHexString(Encoding.ASCII.GetString(data).Trim());
            return key.Length == 32;
        }
        catch
        {
            return false;
        }
    }
}
