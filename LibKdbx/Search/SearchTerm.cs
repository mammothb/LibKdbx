using System.Text.RegularExpressions;

namespace LibKdbx;

/// <summary>
/// Search field identifiers for <see cref="EntrySearcher"/>.
/// Mirrors KeePassXC's <c>EntrySearcher::Field</c>.
/// </summary>
public enum SearchField
{
    /// <summary>No specific field — broad search across title, username, url, tags, notes.</summary>
    Undefined,
    Title,
    Username,
    Password,
    Url,
    Notes,

    /// <summary>Search custom attribute keys and values.</summary>
    AttributeKV,

    /// <summary>Search attachment filenames.</summary>
    Attachment,

    /// <summary>Search a specific custom attribute by key. <see cref="SearchTerm.Word"/> is the key.</summary>
    AttributeValue,

    /// <summary>Search group name or hierarchy path.</summary>
    Group,

    /// <summary>Search entry tags.</summary>
    Tag,

    /// <summary>Property check: is:expired, is:weak.</summary>
    Is,

    /// <summary>Property check: has:totp.</summary>
    Has,

    /// <summary>Search by UUID hex string.</summary>
    Uuid,
}

/// <summary>
/// A single parsed search term with field, word, compiled regex, and exclude flag.
/// Mirrors KeePassXC's <c>EntrySearcher::SearchTerm</c>.
/// </summary>
public readonly record struct SearchTerm(SearchField Field, string Word, Regex Regex, bool Exclude);
