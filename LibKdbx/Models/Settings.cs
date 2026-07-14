using System.Security.Cryptography;
using System.Text;

namespace LibKdbx;

/// <summary>
/// Configuration for creating or opening a KDBX database.
/// </summary>
public class Settings
{
    public KdbxFormat Format { get; set; } = KdbxFormat.Kdbx4;
    public CipherAlgorithm Cipher { get; set; } = CipherAlgorithm.ChaCha20;
    public bool IsCompressed { get; set; } = true;
    public ProtectedStreamAlgorithm InnerStreamAlgorithm { get; set; } =
        ProtectedStreamAlgorithm.ChaCha20;
    public IKdf Kdf { get; set; } = DefaultArgon2id();

    // ── Database-level identities (KDBX 4.x public custom data) ──────────

    /// <summary>Unique identifier for this database.</summary>
    public Guid DatabaseUuid { get; set; } = Guid.NewGuid();

    /// <summary>Public UUID exposed to KeeShare peers.</summary>
    public Guid PublicUuid { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable name shown to KeeShare peers.</summary>
    public string PublicName { get; set; } = "";

    /// <summary>Color associated with this database (e.g. "#FF0000").</summary>
    public string PublicColor { get; set; } = "";

    /// <summary>Icon index for this database.</summary>
    public int PublicIcon { get; set; }

    // ── Internal helpers ──────────────────────────────────────────────────

    internal static Settings FromHeader(IHeader header, ProtectedStreamAlgorithm innerAlgo)
    {
        var settings = new Settings
        {
            Format = header.IsVersion4 ? KdbxFormat.Kdbx4 : KdbxFormat.Kdbx3,
            Cipher = SymmetricCipher.FromUuid(header.CipherId),
            IsCompressed = header.IsCompressed,
            InnerStreamAlgorithm = innerAlgo,
            Kdf = header.CreateKdf(),
        };

        // Parse public custom data from header if present (KDBX 4.x only)
        if (header is KdbxHeader kh && kh.PublicCustomData is { Length: > 0 } data)
        {
            ParsePublicCustomData(Encoding.UTF8.GetString(data), settings);
        }

        return settings;
    }

    /// <summary>
    /// Validates the settings and builds a fresh <see cref="KdbxHeader"/>
    /// (new random MasterSeed, IV, etc.).
    /// </summary>
    internal KdbxHeader ToHeader()
    {
        KdbxHeader header;

        if (Format == KdbxFormat.Kdbx4)
        {
            header = KdbxHeader.CreateNewV4(Cipher, Kdf, IsCompressed);

            // Serialize public custom data
            string pubData = BuildPublicCustomData();
            if (pubData.Length > 0)
            {
                header.PublicCustomData = Encoding.UTF8.GetBytes(pubData);
            }
        }
        else
        {
            if (Kdf is not AesKdf aesKdf)
            {
                throw new InvalidOperationException(
                    "KDBX 3.x only supports AES-KDF. Set Kdf to an AesKdf instance."
                );
            }

            header = KdbxHeader.CreateNewV3(
                Cipher,
                InnerStreamAlgorithm,
                aesKdf.Rounds,
                IsCompressed
            );
        }

        return header;
    }

    internal static Argon2Kdf DefaultArgon2id() =>
        new(
            salt: RandomNumberGenerator.GetBytes(32),
            parallelism: 2,
            memoryKib: 64 * 1024,
            iterations: 2,
            type: Argon2Type.Id
        );

    // ── Public custom data serialization ──────────────────────────────────

    private string BuildPublicCustomData()
    {
        var sb = new StringBuilder();

        void Append(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                sb.Append(key).Append(": ").Append(value).Append('\n');
            }
        }

        if (PublicUuid != Guid.Empty)
        {
            Append("PublicUUID", PublicUuid.ToString("D"));
        }
        if (!string.IsNullOrEmpty(PublicName))
        {
            Append("Name", PublicName);
        }
        if (!string.IsNullOrEmpty(PublicColor))
        {
            Append("Color", PublicColor);
        }
        if (PublicIcon != 0)
        {
            Append("Icon", PublicIcon.ToString());
        }

        return sb.ToString();
    }

    private static void ParsePublicCustomData(string data, Settings settings)
    {
        foreach (string line in data.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            int colon = line.IndexOf(':');
            if (colon < 0)
            {
                continue;
            }
            string key = line[..colon].Trim();
            string value = line[(colon + 1)..].Trim();

            switch (key)
            {
                case "PublicUUID":
                    if (Guid.TryParse(value, out Guid uuid))
                    {
                        settings.PublicUuid = uuid;
                    }
                    break;
                case "Name":
                    settings.PublicName = value;
                    break;
                case "Color":
                    settings.PublicColor = value;
                    break;
                case "Icon":
                    if (int.TryParse(value, out int icon))
                    {
                        settings.PublicIcon = icon;
                    }
                    break;
            }
        }
    }
}
