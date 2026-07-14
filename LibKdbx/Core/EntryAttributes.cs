using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace LibKdbx;

/// <summary>
/// Stores an entry's attributes: five default keys (Title, UserName, Password, URL, Notes)
/// plus arbitrary custom attributes. Per-key protection flags. Reference detection.
/// Mirrors KeePassXC's EntryAttributes.
/// </summary>
public partial class EntryAttributes
{
    // ── Default attribute keys ──────────────────────────────────────────────

    public const string TitleKey = "Title";
    public const string UserNameKey = "UserName";
    public const string PasswordKey = "Password";
    public const string URLKey = "URL";
    public const string NotesKey = "Notes";

    public static readonly IReadOnlyList<string> DefaultAttributeKeys =
    [
        TitleKey,
        UserNameKey,
        PasswordKey,
        URLKey,
        NotesKey,
    ];

    // ── Additional URL prefix ───────────────────────────────────────────────

    public const string AdditionalUrlPrefix = "KP2A_URL";

    // ── Passkey constants ───────────────────────────────────────────────────

    public const string PasskeyAttributePrefix = "KPEX_PASSKEY";
    public const string KPEX_PASSKEY_USERNAME = "KPEX_PASSKEY_USERNAME";
    public const string KPEX_PASSKEY_CREDENTIAL_ID = "KPEX_PASSKEY_CREDENTIAL_ID";
    public const string KPEX_PASSKEY_GENERATED_USER_ID = "KPEX_PASSKEY_GENERATED_USER_ID";
    public const string KPEX_PASSKEY_PRIVATE_KEY_PEM = "KPEX_PASSKEY_PRIVATE_KEY_PEM";
    public const string KPEX_PASSKEY_RELYING_PARTY = "KPEX_PASSKEY_RELYING_PARTY";
    public const string KPEX_PASSKEY_USER_HANDLE = "KPEX_PASSKEY_USER_HANDLE";
    public const string KPXC_PASSKEY_USERNAME = "KPXC_PASSKEY_USERNAME";

    // ── Storage ─────────────────────────────────────────────────────────────

    private readonly Dictionary<string, string> _attributes = [];
    private readonly HashSet<string> _protectedKeys = new(StringComparer.Ordinal);

    // ── Constructor ─────────────────────────────────────────────────────────

    public EntryAttributes()
    {
        Clear();
    }

    // ── Default attribute properties ────────────────────────────────────────

    public string Title
    {
        get => _attributes[TitleKey];
        set => Set(TitleKey, value);
    }
    public string UserName
    {
        get => _attributes[UserNameKey];
        set => Set(UserNameKey, value);
    }
    public string Password
    {
        get => _attributes[PasswordKey];
        set => Set(PasswordKey, value);
    }
    public string Url
    {
        get => _attributes[URLKey];
        set => Set(URLKey, value);
    }
    public string Notes
    {
        get => _attributes[NotesKey];
        set => Set(NotesKey, value);
    }

    // ── Keys ────────────────────────────────────────────────────────────────

    /// <summary>All attribute keys (default + custom) in insertion order.</summary>
    public IReadOnlyList<string> Keys => _attributes.Keys.ToList();

    /// <summary>Non-default, non-passkey keys.</summary>
    public IReadOnlyList<string> CustomKeys =>
        Keys.Where(k => !IsDefaultAttribute(k) && !IsPasskeyAttribute(k)).ToList();

    // ── Get / Set / Remove ──────────────────────────────────────────────────

    /// <summary>Gets the value for <paramref name="key"/>, or null if not found.</summary>
    public string? Get(string key) =>
        _attributes.TryGetValue(key, out string? value) ? value : null;

    /// <summary>
    /// Gets the value for <paramref name="key"/>.
    /// Returns true if found, false otherwise.
    /// </summary>
    public bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
        _attributes.TryGetValue(key, out value);

    /// <summary>True if the key exists.</summary>
    public bool Contains(string key) => _attributes.ContainsKey(key);

    /// <summary>True if any attribute has the given value.</summary>
    public bool ContainsValue(string value) =>
        _attributes.Values.Contains(value, StringComparer.Ordinal);

    /// <summary>
    /// Sets <paramref name="key"/> to <paramref name="value"/>.
    /// Default keys cannot be created or removed — they always exist.
    /// </summary>
    public void Set(string key, string value, bool protect = false)
    {
        bool isDefault = IsDefaultAttribute(key);
        if (!isDefault && !_attributes.ContainsKey(key))
        {
            // Adding a new custom key — KeePassXC would emit aboutToBeAdded
        }

        _attributes[key] = value;

        if (protect)
        {
            _protectedKeys.Add(key);
        }
        else
        {
            _protectedKeys.Remove(key);
        }
    }

    /// <summary>Removes a custom key. Default keys cannot be removed.</summary>
    public bool Remove(string key)
    {
        if (IsDefaultAttribute(key))
        {
            return false;
        }

        _protectedKeys.Remove(key);
        return _attributes.Remove(key);
    }

    /// <summary>Renames a custom key. Default keys cannot be renamed.</summary>
    public bool Rename(string oldKey, string newKey)
    {
        if (IsDefaultAttribute(oldKey) || IsDefaultAttribute(newKey))
        {
            return false;
        }

        if (!_attributes.TryGetValue(oldKey, out string? value))
        {
            return false;
        }

        if (_attributes.ContainsKey(newKey))
        {
            return false;
        }

        bool wasProtected = _protectedKeys.Remove(oldKey);

        _attributes.Remove(oldKey);
        _attributes[newKey] = value;
        if (wasProtected)
        {
            _protectedKeys.Add(newKey);
        }

        return true;
    }

    // ── Protection ──────────────────────────────────────────────────────────

    /// <summary>True if this key's value is memory-protected.</summary>
    public bool IsProtected(string key) => _protectedKeys.Contains(key);

    // ── References ──────────────────────────────────────────────────────────

    /// <summary>True if the value for <paramref name="key"/> is a field reference.</summary>
    public bool IsReference(string key)
    {
        string? value = Get(key);
        return value is not null && RefRegex().IsMatch(value);
    }

    /// <summary>
    /// Extracts the target UUID from a reference value.
    /// Returns <see cref="Guid.Empty"/> if the value is not a reference or has no UUID search target.
    /// </summary>
    public Guid ReferenceUuid(string key)
    {
        string? value = Get(key);
        if (value is null)
        {
            return Guid.Empty;
        }

        Match match = RefRegex().Match(value);
        if (!match.Success)
        {
            return Guid.Empty;
        }

        string searchIn = match.Groups["SearchIn"].Value.ToUpperInvariant();
        // Only UUID search targets contain a usable UUID
        if (searchIn != "I")
        {
            return Guid.Empty;
        }

        string hex = match.Groups["SearchText"].Value;
        return TryParseHexGuid(hex, out Guid guid) ? guid : Guid.Empty;
    }

    // ── Passkey ─────────────────────────────────────────────────────────────

    /// <summary>True if this entry has passkey attributes.</summary>
    public bool HasPasskey() => Keys.Any(IsPasskeyAttribute);

    /// <summary>Removes all passkey attributes.</summary>
    public void RemovePasskeyAttributes()
    {
        foreach (string key in Keys.Where(IsPasskeyAttribute).ToList())
        {
            Remove(key);
        }
    }

    // ── Additional URLs ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the primary URL (the URL attribute) if set,
    /// otherwise the first additional URL. Returns null if there are no URLs.
    /// </summary>
    public string? ResolveUrl()
    {
        if (!string.IsNullOrEmpty(Url))
        {
            return Url;
        }

        IReadOnlyList<string> additional = GetAdditionalUrls();
        return additional.Count > 0 ? additional[0] : null;
    }

    /// <summary>Returns all URLs: the primary URL attribute plus any KP2A_URL keys.</summary>
    public IReadOnlyList<string> GetAllUrls()
    {
        var urls = new List<string>();
        if (!string.IsNullOrEmpty(Url))
        {
            urls.Add(Url);
        }

        urls.AddRange(GetAdditionalUrls());
        return urls;
    }

    /// <summary>Returns additional URLs (KP2A_URL_1, KP2A_URL_2, etc.), excluding the primary URL.</summary>
    public IReadOnlyList<string> GetAdditionalUrls()
    {
        return Keys.Where(k =>
                k.StartsWith(AdditionalUrlPrefix, StringComparison.OrdinalIgnoreCase)
                && k != AdditionalUrlPrefix
            )
            .Select(k => _attributes[k])
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();
    }

    // ── Bulk operations ─────────────────────────────────────────────────────

    /// <summary>Resets to empty default attributes. Removes all custom keys.</summary>
    public void Clear()
    {
        _attributes.Clear();
        _protectedKeys.Clear();
        foreach (string key in DefaultAttributeKeys)
        {
            _attributes[key] = "";
        }
    }

    /// <summary>
    /// Replaces all custom attributes with those from <paramref name="other"/>.
    /// Default attribute values are unchanged.
    /// </summary>
    public void CopyCustomKeysFrom(EntryAttributes other)
    {
        // Remove existing custom keys
        foreach (string key in Keys.Where(k => !IsDefaultAttribute(k)).ToList())
        {
            _attributes.Remove(key);
            _protectedKeys.Remove(key);
        }
        // Copy from other
        foreach (string key in other.Keys.Where(k => !IsDefaultAttribute(k)))
        {
            _attributes[key] = other._attributes[key];
            if (other._protectedKeys.Contains(key))
            {
                _protectedKeys.Add(key);
            }
        }
    }

    /// <summary>True if custom keys differ from <paramref name="other"/>.</summary>
    public bool AreCustomKeysDifferent(EntryAttributes other)
    {
        var customKeys = new HashSet<string>(CustomKeys);
        var otherCustomKeys = new HashSet<string>(other.CustomKeys);
        if (!customKeys.SetEquals(otherCustomKeys))
        {
            return true;
        }

        foreach (string key in customKeys)
        {
            if (_attributes[key] != other._attributes[key])
            {
                return true;
            }

            if (_protectedKeys.Contains(key) != other._protectedKeys.Contains(key))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Deep copy from <paramref name="other"/>.</summary>
    public void CopyFrom(EntryAttributes other)
    {
        _attributes.Clear();
        _protectedKeys.Clear();
        foreach ((string key, string value) in other._attributes)
        {
            _attributes[key] = value;
            if (other._protectedKeys.Contains(key))
            {
                _protectedKeys.Add(key);
            }
        }
    }

    /// <summary>Creates an independent copy.</summary>
    public EntryAttributes Clone()
    {
        EntryAttributes clone = new();
        clone.CopyFrom(this);
        return clone;
    }

    // ── Equality ────────────────────────────────────────────────────────────

    public override bool Equals(object? obj) =>
        obj is EntryAttributes other
        && _attributes.Count == other._attributes.Count
        && _attributes.All(kv =>
            other._attributes.TryGetValue(kv.Key, out string? v) && v == kv.Value
        )
        && _protectedKeys.SetEquals(other._protectedKeys);

    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach ((string k, string v) in _attributes)
        {
            hash.Add(k);
            hash.Add(v);
            hash.Add(_protectedKeys.Contains(k));
        }
        return hash.ToHashCode();
    }

    // ── Static helpers ──────────────────────────────────────────────────────

    public static bool IsDefaultAttribute(string key) =>
        DefaultAttributeKeys.Contains(key, StringComparer.OrdinalIgnoreCase);

    public static bool IsPasskeyAttribute(string key) =>
        key.StartsWith(PasskeyAttributePrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Regex matching {REF:W@S:text} patterns.</summary>
    [GeneratedRegex(
        @"\{REF:(?<WantedField>[TUPANI])@(?<SearchIn>[TUPANIO]):(?<SearchText>(?:[^{}]|\{[^}]*\})+)\}",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex RefRegex();

    private static bool TryParseHexGuid(string hex, out Guid result)
    {
        result = Guid.Empty;
        if (hex.Length != 32)
        {
            return false;
        }
        string formatted = $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
        return Guid.TryParse(formatted, out result);
    }
}
