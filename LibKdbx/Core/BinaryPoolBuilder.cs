using System.Security.Cryptography;

namespace LibKdbx;

/// <summary>
/// Builds a deduplicated binary pool from all attachments in a Group tree.
/// Uses SHA-256 hashing for O(N × attachment_size) instead of linear scan.
/// Mirrors the logic that will live in KdbxXmlWriter (Phase 1g).
/// </summary>
public static class BinaryPoolBuilder
{
    /// <summary>
    /// Collects all unique attachment data from <paramref name="root"/> and its descendants.
    /// Returns a list where each entry is a unique byte array, and a lookup from
    /// data hash to pool index for writing binary references.
    /// </summary>
    public static BinaryPool Build(Group root)
    {
        Dictionary<string, int> hashToIndex = []; // SHA-256 hex → pool index
        List<byte[]> pool = [];

        CollectGroup(root, pool, hashToIndex);

        return new BinaryPool(pool, hashToIndex);
    }

    private static void CollectGroup(Group group, List<byte[]> pool, Dictionary<string, int> hashToIndex)
    {
        foreach (Entry entry in group.Entries)
        {
            CollectEntry(entry, pool, hashToIndex);
            foreach (Entry hist in entry.History)
            {
                CollectEntry(hist, pool, hashToIndex);
            }
        }

        foreach (Group sub in group.Groups)
        {
            CollectGroup(sub, pool, hashToIndex);
        }
    }

    private static void CollectEntry(Entry entry, List<byte[]> pool, Dictionary<string, int> hashToIndex)
    {
        foreach (string key in entry.Attachments.Keys)
        {
            byte[]? data = entry.Attachments.Get(key);
            if (data is null || data.Length == 0)
            {
                continue;
            }

            string hash = Convert.ToHexString(SHA256.HashData(data));
            if (!hashToIndex.ContainsKey(hash))
            {
                hashToIndex[hash] = pool.Count;
                pool.Add(data);
            }
        }
    }
}

/// <summary>
/// Result of building a binary pool: unique data items and a hash→index lookup.
/// </summary>
public sealed class BinaryPool
{
    public IReadOnlyList<byte[]> Items { get; }
    private readonly Dictionary<string, int> _hashToIndex;

    internal BinaryPool(List<byte[]> items, Dictionary<string, int> hashToIndex)
    {
        Items = items;
        _hashToIndex = hashToIndex;
    }

    /// <summary>Gets the pool index for <paramref name="data"/>, or -1 if not found.</summary>
    public int GetIndex(byte[] data)
    {
        if (data.Length == 0)
        {
            return -1;
        }

        string hash = Convert.ToHexString(SHA256.HashData(data));
        return _hashToIndex.GetValueOrDefault(hash, -1);
    }

    /// <summary>Number of unique items in the pool.</summary>
    public int Count => Items.Count;
}
