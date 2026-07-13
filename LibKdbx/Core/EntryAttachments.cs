namespace LibKdbx;

/// <summary>
/// Key-value map of attachment filenames to binary data.
/// Replaces DgNet's flat <c>List&lt;EntryBinary&gt;</c>.
/// Mirrors KeePassXC's EntryAttachments.
/// </summary>
public class EntryAttachments
{
    private readonly Dictionary<string, byte[]> _attachments = new(StringComparer.Ordinal);

    /// <summary>All attachment filenames in insertion order.</summary>
    public IReadOnlyList<string> Keys => _attachments.Keys.ToList();

    /// <summary>Number of attachments.</summary>
    public int Count => _attachments.Count;

    /// <summary>True when there are no attachments.</summary>
    public bool IsEmpty => _attachments.Count == 0;

    /// <summary>Gets the data for <paramref name="key"/>, or null if not found.</summary>
    public byte[]? Get(string key) => _attachments.TryGetValue(key, out byte[]? data) ? data : null;

    /// <summary>True if the attachment exists.</summary>
    public bool Contains(string key) => _attachments.ContainsKey(key);

    /// <summary>
    /// Sets (or replaces) the attachment <paramref name="key"/> to the given <paramref name="data"/>.
    /// </summary>
    public void Set(string key, byte[] data)
    {
        _attachments[key] = data;
    }

    /// <summary>Removes the attachment if it exists.</summary>
    public bool Remove(string key) => _attachments.Remove(key);

    /// <summary>Renames an attachment. Fails if oldKey doesn't exist or newKey already exists.</summary>
    public bool Rename(string oldKey, string newKey)
    {
        if (!_attachments.TryGetValue(oldKey, out byte[]? data))
        {
            return false;
        }

        if (_attachments.ContainsKey(newKey))
        {
            return false;
        }

        _attachments.Remove(oldKey);
        _attachments[newKey] = data;
        return true;
    }

    /// <summary>Removes all attachments.</summary>
    public void Clear() => _attachments.Clear();

    /// <summary>Total byte size of all attachment data.</summary>
    public int DataSize() => _attachments.Values.Sum(d => d.Length);

    /// <summary>Deep copy from <paramref name="other"/>.</summary>
    public void CopyFrom(EntryAttachments other)
    {
        _attachments.Clear();
        foreach ((string key, byte[] data) in other._attachments)
        {
            byte[] copy = new byte[data.Length];
            Array.Copy(data, copy, data.Length);
            _attachments[key] = copy;
        }
    }

    /// <summary>Creates an independent copy.</summary>
    public EntryAttachments Clone()
    {
        EntryAttachments clone = new();
        clone.CopyFrom(this);
        return clone;
    }

    // ── Equality ────────────────────────────────────────────────────────────

    public override bool Equals(object? obj) =>
        obj is EntryAttachments other
        && _attachments.Count == other._attachments.Count
        && _attachments.All(kv =>
            other._attachments.TryGetValue(kv.Key, out byte[]? otherData)
            && otherData.AsSpan().SequenceEqual(kv.Value)
        );

    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach ((string key, byte[] data) in _attachments)
        {
            hash.Add(key);
            hash.AddBytes(data);
        }
        return hash.ToHashCode();
    }
}
