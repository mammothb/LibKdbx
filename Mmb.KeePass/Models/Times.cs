namespace Mmb.KeePass;

public class Times
{
    public DateTime CreationTime { get; set; }
    public DateTime LastModificationTime { get; set; }
    public DateTime LastAccessTime { get; set; }
    public DateTime ExpiryTime { get; set; }
    public bool Expires { get; set; }
    public int UsageCount { get; set; }
    public DateTime LocationChanged { get; set; }

    public static Times Create()
    {
        DateTime now = DateTime.UtcNow;
        return new Times { CreationTime = now, LastModificationTime = now };
    }

    public Times Clone() =>
        new()
        {
            CreationTime = CreationTime,
            LastModificationTime = LastModificationTime,
            LastAccessTime = LastAccessTime,
            ExpiryTime = ExpiryTime,
            Expires = Expires,
            UsageCount = UsageCount,
            LocationChanged = LocationChanged,
        };
}
