namespace LibKdbx;

/// <summary>
/// Arbitrary key-value metadata attached to Metadata, Groups, and Entries.
/// Used by KeePassXC for browser integration, KeeShare, FdoSecrets, and
/// other plugin data. Mirrors KeePassXC's CustomData.
/// </summary>
public class CustomData
{
    private readonly Dictionary<string, CustomDataItem> _data = [];

    /// <summary>All keys in insertion order.</summary>
    public IReadOnlyList<string> Keys => _data.Keys.ToList();

    /// <summary>Number of items.</summary>
    public int Count => _data.Count;

    /// <summary>True when there are no items.</summary>
    public bool IsEmpty => _data.Count == 0;

    /// <summary>
    /// Gets the raw item for <paramref name="key"/>, or null if not found.
    /// </summary>
    public CustomDataItem? GetItem(string key)
    {
        return _data.TryGetValue(key, out var item) ? item : null;
    }

    /// <summary>
    /// Gets the value for <paramref name="key"/>, or null if not found.
    /// </summary>
    public string? GetValue(string key)
    {
        return _data.TryGetValue(key, out var item) ? item.Value : null;
    }

    /// <summary>True if the key exists.</summary>
    public bool ContainsKey(string key) => _data.ContainsKey(key);

    /// <summary>
    /// Sets <paramref name="key"/> to the given <paramref name="value"/>,
    /// with an optional <paramref name="lastModified"/> timestamp.
    /// </summary>
    public void Set(string key, string value, DateTime? lastModified = null)
    {
        _data[key] = new CustomDataItem(value, lastModified);
    }

    /// <summary>Removes the key if it exists.</summary>
    public bool Remove(string key) => _data.Remove(key);

    /// <summary>Removes all items.</summary>
    public void Clear() => _data.Clear();

    /// <summary>
    /// Deep copy from another instance. Clears existing data first.
    /// </summary>
    public void CopyFrom(CustomData other)
    {
        _data.Clear();
        foreach (var (key, item) in other._data)
        {
            _data[key] = item;
        }
    }

    /// <summary>
    /// Indexer: gets or sets the value. Setting always uses null LastModified.
    /// Use <see cref="Set"/> to provide a timestamp.
    /// </summary>
    public string? this[string key]
    {
        get => GetValue(key);
        set
        {
            if (value is null)
            {
                Remove(key);
            }
            else
            {
                _data[key] = new CustomDataItem(value);
            }
        }
    }
}
