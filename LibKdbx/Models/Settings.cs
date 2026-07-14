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
    public IKdf Kdf { get; set; } = Argon2Kdf.CreateDefault();

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
}
