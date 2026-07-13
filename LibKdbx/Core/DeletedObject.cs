namespace LibKdbx;

/// <summary>
/// Represents an entry or group permanently deleted from the recycle bin.
/// KeePassXC records these in &lt;Root&gt;&lt;DeletedObjects&gt; to prevent
/// UUID reuse and track deletion times.
/// </summary>
public readonly record struct DeletedObject(Guid Uuid, DateTime DeletionTime);
