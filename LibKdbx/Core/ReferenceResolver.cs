namespace LibKdbx;

/// <summary>
/// Resolves KeePass field references (<c>{REF:W@S:text}</c>) within a database's entry index.
/// </summary>
internal static class ReferenceResolver
{
    internal static string ResolveField(
        Dictionary<Guid, Entry> index,
        Entry entry,
        string fieldName,
        int maxDepth = 10
    )
    {
        string? value = entry.Attributes.Get(fieldName) ?? "";
        return ResolveValue(index, value, maxDepth);
    }

    private static string ResolveValue(Dictionary<Guid, Entry> index, string value, int depth)
    {
        if (depth <= 0 || !FieldReference.TryParse(value, out FieldReference refInfo))
        {
            return value;
        }

        Entry? target = FindReferencedEntry(index, refInfo);
        if (target is null)
        {
            return value;
        }

        // WantedField 'I' returns the target entry's UUID as a hex string
        if (refInfo.WantedField == 'I')
        {
            return target.Uuid.ToString("N");
        }

        string? fieldKey = FieldReference.FieldCodeToKey(refInfo.WantedField);
        if (fieldKey is null)
        {
            return value;
        }

        if (!target.Attributes.TryGetValue(fieldKey, out string? resolved))
        {
            resolved = "";
        }
        return ResolveValue(index, resolved, depth - 1);
    }

    private static Entry? FindReferencedEntry(Dictionary<Guid, Entry> index, FieldReference refInfo)
    {
        if (refInfo.SearchIn == 'I')
        {
            if (GuidRfc4122.TryParseHex(refInfo.SearchValue, out Guid uuid))
            {
                return index.GetValueOrDefault(uuid);
            }
            return null;
        }

        if (refInfo.SearchIn == 'O')
        {
            return index.Values.FirstOrDefault(e =>
                e.Attributes.ContainsValue(refInfo.SearchValue)
            );
        }

        string? fieldKey = FieldReference.FieldCodeToKey(refInfo.SearchIn);
        if (fieldKey is null)
        {
            return null;
        }

        return index.Values.FirstOrDefault(e => e.Attributes.Get(fieldKey) == refInfo.SearchValue);
    }
}
