using System.Text.RegularExpressions;

namespace LibKdbx;

/// <summary>
/// Advanced KeePassXC-compatible entry searcher with field-specific query parsing,
/// wildcard/regex matching, and exclude modifiers.
/// Ported from KeePassXC's <c>EntrySearcher</c>.
/// </summary>
public partial class EntrySearcher(bool caseSensitive = false, bool skipProtected = false)
{
    public bool CaseSensitive { get; set; } = caseSensitive;
    public bool SkipProtected { get; set; } = skipProtected;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Search all entries under <paramref name="baseGroup"/> (recursive).</summary>
    public List<Entry> Search(string searchString, Group baseGroup, bool forceSearch = false)
    {
        List<SearchTerm> terms = ParseSearchTerms(searchString);
        return Search(terms, baseGroup, forceSearch);
    }

    /// <summary>Search using pre-parsed terms.</summary>
    public List<Entry> Search(List<SearchTerm> terms, Group baseGroup, bool forceSearch = false)
    {
        List<Entry> results = [];
        foreach (Group group in baseGroup.GroupsRecursive(true))
        {
            if (forceSearch || group.ResolveSearchingEnabled())
            {
                foreach (Entry entry in group.Entries)
                {
                    if (MatchesAll(entry, terms))
                    {
                        results.Add(entry);
                    }
                }
            }
        }
        return results;
    }

    /// <summary>Search a specific list of entries.</summary>
    public List<Entry> SearchEntries(string searchString, IReadOnlyList<Entry> entries)
    {
        List<SearchTerm> terms = ParseSearchTerms(searchString);
        return SearchEntries(terms, entries);
    }

    /// <summary>Search a specific list of entries using pre-parsed terms.</summary>
    public List<Entry> SearchEntries(List<SearchTerm> terms, IReadOnlyList<Entry> entries)
    {
        List<Entry> results = [];
        foreach (Entry entry in entries)
        {
            if (MatchesAll(entry, terms))
            {
                results.Add(entry);
            }
        }
        return results;
    }

    // ── Query parsing ────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a KeePassXC-style search string into a list of <see cref="SearchTerm"/>.
    /// </summary>
    public List<SearchTerm> ParseSearchTerms(string searchString)
    {
        List<SearchTerm> terms = [];
        MatchCollection matches = TermParserRegex().Matches(searchString);

        foreach (Match result in matches)
        {
            // Quoted string (group 3) or unquoted word (group 4)
            string word = result.Groups[3].Value;
            if (word.Length > 0)
            {
                word = word.Replace("\\\"", "\"");
            }
            else
            {
                word = result.Groups[4].Value;
            }

            if (word.Length == 0)
            {
                continue;
            }

            string mods = result.Groups[1].Value;
            string fieldName = result.Groups[2].Value;

            // Build regex
            RegexConvertOptions opts = CaseSensitive
                ? RegexConvertOptions.CaseSensitive
                : RegexConvertOptions.None;
            bool hasStarMod = mods.Contains('*');
            if (!hasStarMod)
            {
                opts |= RegexConvertOptions.WildcardAll;
            }
            if (mods.Contains('+'))
            {
                opts |= RegexConvertOptions.ExactMatch;
            }
            Regex regex = RegexHelper.ConvertToRegex(word, opts);

            bool exclude = mods.Contains('-') || mods.Contains('!');

            // Determine field
            SearchField field = SearchField.Undefined;

            if (fieldName.Length > 0)
            {
                if (fieldName.StartsWith('_'))
                {
                    field = SearchField.AttributeValue;
                    // Word becomes the attribute key, regex is used for value match
                    word = fieldName[1..];
                }
                else
                {
                    field = MatchFieldName(fieldName);
                }
            }

            terms.Add(new SearchTerm(field, word, regex, exclude));
        }

        return terms;
    }

    // ── Private matching ─────────────────────────────────────────────────────

    /// <summary>True if <paramref name="entry"/> matches ALL non-excluded terms.</summary>
    private bool MatchesAll(Entry entry, List<SearchTerm> terms)
    {
        // Empty terms: match everything (unless skipProtected, then match nothing)
        bool overallMatch = !SkipProtected;

        foreach (SearchTerm term in terms)
        {
            bool found = MatchSingle(entry, term);

            // Negate if exclude
            found = (found && !term.Exclude) || (!found && term.Exclude);

            // Short-circuit: all terms must match (AND)
            if (!found)
            {
                return false;
            }
            overallMatch = true;
        }

        return overallMatch;
    }

    private bool MatchSingle(Entry entry, SearchTerm term)
    {
        switch (term.Field)
        {
            case SearchField.Title:
                return term.Regex.IsMatch(ResolveTitle(entry));

            case SearchField.Username:
                return term.Regex.IsMatch(ResolveUserName(entry));

            case SearchField.Password:
                if (SkipProtected)
                {
                    return false;
                }
                return term.Regex.IsMatch(ResolvePassword(entry));

            case SearchField.Url:
                return term.Regex.IsMatch(ResolveUrl(entry));

            case SearchField.Notes:
                return term.Regex.IsMatch(entry.Notes);

            case SearchField.AttributeKV:
            {
                // Match against any custom attribute key or value
                foreach (string key in entry.Attributes.CustomKeys)
                {
                    if (term.Regex.IsMatch(key))
                    {
                        return true;
                    }
                    string? val = entry.Attributes.Get(key);
                    if (val is not null && term.Regex.IsMatch(val))
                    {
                        return true;
                    }
                }
                return false;
            }

            case SearchField.Attachment:
            {
                foreach (string filename in entry.Attachments.Keys)
                {
                    if (term.Regex.IsMatch(filename))
                    {
                        return true;
                    }
                }
                return false;
            }

            case SearchField.AttributeValue:
            {
                if (SkipProtected && entry.Attributes.IsProtected(term.Word))
                {
                    return false;
                }
                string? val = entry.Attributes.Get(term.Word);
                return val is not null && term.Regex.IsMatch(val);
            }

            case SearchField.Group:
            {
                if (term.Word.Contains('/'))
                {
                    string hierarchy = string.Join("/", entry.ParentGroup?.Hierarchy() ?? []);
                    hierarchy = "/" + hierarchy;
                    return term.Regex.IsMatch(hierarchy);
                }
                else
                {
                    return entry.ParentGroup is not null
                        && term.Regex.IsMatch(entry.ParentGroup.Name);
                }
            }

            case SearchField.Tag:
            {
                string[] tags = entry.Tags.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (string tag in tags)
                {
                    if (term.Regex.IsMatch(tag.Trim()))
                    {
                        return true;
                    }
                }
                return false;
            }

            case SearchField.Is:
            {
                if (term.Word.StartsWith("expired", StringComparison.OrdinalIgnoreCase))
                {
                    int days = 0;
                    string[] parts = term.Word.Split('-');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int parsedDays))
                    {
                        days = parsedDays;
                    }
                    return entry.WillExpireInDays(days) && !entry.IsRecycled();
                }
                // is:weak — defer until PasswordHealth is ported
                return false;
            }

            case SearchField.Has:
            {
                if (term.Word.Equals("totp", StringComparison.OrdinalIgnoreCase))
                {
                    return entry.HasTotp();
                }
                return false;
            }

            case SearchField.Uuid:
            {
                return term.Regex.IsMatch(entry.Uuid.ToString("N"));
            }

            default: // Undefined — broad search
            {
                return term.Regex.IsMatch(ResolveTitle(entry))
                    || term.Regex.IsMatch(ResolveUserName(entry))
                    || term.Regex.IsMatch(ResolveUrl(entry))
                    || MatchAnyTag(entry, term.Regex)
                    || term.Regex.IsMatch(entry.Notes);
            }
        }
    }

    // ── Placeholder-aware value resolvers ────────────────────────────────────

    private static string ResolveTitle(Entry entry) =>
        PlaceholderResolver.Resolve(entry, entry.Title);

    private static string ResolveUserName(Entry entry) =>
        PlaceholderResolver.Resolve(entry, entry.UserName);

    private static string ResolvePassword(Entry entry) =>
        PlaceholderResolver.Resolve(entry, entry.Password);

    private static string ResolveUrl(Entry entry) => PlaceholderResolver.Resolve(entry, entry.Url);

    private static bool MatchAnyTag(Entry entry, Regex regex)
    {
        string[] tags = entry.Tags.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (string tag in tags)
        {
            if (regex.IsMatch(tag.Trim()))
            {
                return true;
            }
        }
        return false;
    }

    // ── Field name matching ──────────────────────────────────────────────────

    private static SearchField MatchFieldName(string fieldName)
    {
        // Ordered so shorter prefixes like "t" don't capture "tag" before "title"
        return fieldName.ToLowerInvariant() switch
        {
            "attachment" => SearchField.Attachment,
            "attribute" => SearchField.AttributeKV,
            "notes" => SearchField.Notes,
            "pw" => SearchField.Password,
            "password" => SearchField.Password,
            "title" => SearchField.Title,
            "username" => SearchField.Username,
            "url" => SearchField.Url,
            "group" => SearchField.Group,
            "tag" => SearchField.Tag,
            "is" => SearchField.Is,
            "has" => SearchField.Has,
            "uuid" => SearchField.Uuid,
            _ => SearchField.Undefined,
        };
    }

    // ── Regex ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Regex matching KeePassXC search terms.
    /// Group 1 = modifiers, Group 2 = field name, Group 3 = quoted string, Group 4 = unquoted string.
    /// </summary>
    [GeneratedRegex(
        @"([-!*+]+)?(?:(\w*):)?(?:(?="")""((?:[^""\\]|\\.)*)""|([^ ]*))( |$)",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex TermParserRegex();
}
