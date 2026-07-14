namespace LibKdbx;

/// <summary>
/// Resolves KeePassXC-style placeholders like <c>{TITLE}</c>, <c>{URL}</c>, <c>{S:attr}</c>,
/// <c>{REF:…}</c>, URL decomposition, datetime, and <c>{DB_DIR}</c>.
/// Recursive resolution with depth limit of 10. Ported from KeePassXC's
/// <c>Entry::resolveMultiplePlaceholders</c>.
/// </summary>
public static class PlaceholderResolver
{
    private const int MaxDepth = 10;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves all placeholders in <paramref name="input"/> using <paramref name="entry"/>'s data.
    /// </summary>
    public static string Resolve(Entry entry, string input, int maxDepth = MaxDepth)
    {
        return ResolveRecursive(entry, input, maxDepth);
    }

    /// <summary>Classifies a <c>{…}</c> token into its placeholder type.</summary>
    internal static PlaceholderType Classify(string placeholder)
    {
        return ClassifyPlaceholder(placeholder);
    }

    // ── Recursive resolution ─────────────────────────────────────────────────

    private static string ResolveRecursive(Entry entry, string input, int depth)
    {
        if (--depth < 0)
        {
            return input;
        }

        // Handle escaped braces: \{…\} → literal {…}
        if (input.StartsWith("\\{") && input.EndsWith("\\}"))
        {
            return "{" + input[2..^2] + "}";
        }

        // Find outermost {…} tokens and resolve them
        return ReplacePlaceholders(entry, input, depth);
    }

    private static string ReplacePlaceholders(Entry entry, string input, int depth)
    {
        var output = new System.Text.StringBuilder(input.Length * 2);

        int pos = 0;
        while (pos < input.Length)
        {
            // Find next '{'
            int open = input.IndexOf('{', pos);
            if (open < 0)
            {
                output.Append(input.AsSpan(pos));
                break;
            }

            // Copy text before the '{'
            output.Append(input.AsSpan(pos, open - pos));

            // Find matching '}' handling nesting
            int close = FindMatchingBrace(input, open);
            if (close < 0)
            {
                // Unmatched brace — treat as literal
                output.Append(input[open]);
                pos = open + 1;
                continue;
            }

            string token = input[open..(close + 1)];
            string resolved = ResolveToken(entry, token, depth);
            output.Append(resolved);
            pos = close + 1;
        }

        return output.ToString();
    }

    /// <summary>
    /// Finds the matching '}' for an opening '{' at <paramref name="openPos"/>,
    /// handling nested braces. Returns -1 if unmatched.
    /// </summary>
    private static int FindMatchingBrace(string input, int openPos)
    {
        int depth = 1;
        for (int i = openPos + 1; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }
        return -1;
    }

    private static string ResolveToken(Entry entry, string token, int depth)
    {
        PlaceholderType type = ClassifyPlaceholder(token);

        switch (type)
        {
            case PlaceholderType.NotPlaceholder:
                // Token without braces — resolve recursively
                return ResolveRecursive(entry, token, depth);

            case PlaceholderType.Unknown:
                // Unknown placeholder: resolve inner content, wrap back in braces
                string inner = token[1..^1]; // strip { and }
                string resolved = ResolveRecursive(entry, inner, depth);
                return "{" + resolved + "}";

            case PlaceholderType.Title:
                return ResolveRecursive(entry, entry.Title, depth);

            case PlaceholderType.UserName:
                return ResolveRecursive(entry, entry.UserName, depth);

            case PlaceholderType.Password:
                return ResolveRecursive(entry, entry.Password, depth);

            case PlaceholderType.Notes:
                return ResolveRecursive(entry, entry.Notes, depth);

            case PlaceholderType.Url:
                return ResolveRecursive(entry, entry.Url, depth);

            case PlaceholderType.Uuid:
                return entry.Uuid.ToString("N");

            case PlaceholderType.DbDir:
                string? filePath = entry.Database?.DatabaseFile?.FullName;
                if (filePath is null)
                {
                    return "";
                }
                return Path.GetDirectoryName(filePath) ?? "";

            case PlaceholderType.CustomAttribute:
                // {S:attrkey}
                string key = token[3..^1]; // strip {S: and }
                string? value = entry.Attributes.Get(key);
                if (value is null)
                {
                    return "";
                }
                return ResolveRecursive(entry, value, depth);

            case PlaceholderType.Reference:
                // {REF:W@S:text} — delegate to Database.ResolveField
                if (entry.Database is null)
                {
                    return token;
                }
                // The token IS the value. Database.ResolveField expects a field key
                // and reads the {REF:…} from that field. For direct token resolution,
                // we need to resolve the REF value directly.
                string refInner = token[1..^1]; // strip { and }
                return ResolveReferenceToken(entry, refInner, depth);

            case PlaceholderType.Totp:
                // TODO: TOTP generation (Phase 4)
                return "";

            case PlaceholderType.UrlWithoutScheme:
            case PlaceholderType.UrlScheme:
            case PlaceholderType.UrlHost:
            case PlaceholderType.UrlPort:
            case PlaceholderType.UrlPath:
            case PlaceholderType.UrlQuery:
            case PlaceholderType.UrlFragment:
            case PlaceholderType.UrlUserInfo:
            case PlaceholderType.UrlUserName:
            case PlaceholderType.UrlPassword:
                string resolvedUrl = ResolveRecursive(entry, entry.Url, depth);
                return ResolveUrlPlaceholder(resolvedUrl, type);

            case PlaceholderType.DateTimeSimple:
            case PlaceholderType.DateTimeYear:
            case PlaceholderType.DateTimeMonth:
            case PlaceholderType.DateTimeDay:
            case PlaceholderType.DateTimeHour:
            case PlaceholderType.DateTimeMinute:
            case PlaceholderType.DateTimeSecond:
            case PlaceholderType.DateTimeUtcSimple:
            case PlaceholderType.DateTimeUtcYear:
            case PlaceholderType.DateTimeUtcMonth:
            case PlaceholderType.DateTimeUtcDay:
            case PlaceholderType.DateTimeUtcHour:
            case PlaceholderType.DateTimeUtcMinute:
            case PlaceholderType.DateTimeUtcSecond:
                return ResolveDateTimePlaceholder(type);

            default:
                return token;
        }
    }

    // ── Reference resolution ─────────────────────────────────────────────────

    private static string ResolveReferenceToken(Entry entry, string refInner, int depth)
    {
        if (--depth < 0)
        {
            return "{" + refInner + "}";
        }

        if (!FieldReference.TryParse("{" + refInner + "}", out FieldReference refInfo))
        {
            return "{" + refInner + "}";
        }

        Entry? target = FindReferencedEntry(entry, refInfo);
        if (target is null)
        {
            return "{" + refInner + "}";
        }

        string resolved;
        if (refInfo.WantedField == 'I')
        {
            resolved = target.Uuid.ToString("N");
        }
        else
        {
            string? fieldKey = FieldReference.FieldCodeToKey(refInfo.WantedField);
            if (fieldKey is null)
            {
                return "{" + refInner + "}";
            }
            resolved = target.Attributes.Get(fieldKey) ?? "";
        }

        return ResolveRecursive(entry, resolved, depth);
    }

    private static Entry? FindReferencedEntry(Entry entry, FieldReference refInfo)
    {
        if (entry.Database is null)
        {
            return null;
        }

        if (refInfo.SearchIn == 'I')
        {
            if (
                GuidRfc4122.TryParseHex(refInfo.SearchValue, out Guid uuid)
                && entry.Database.FindEntryByUuid(uuid) is Entry found
            )
            {
                return found;
            }
            return null;
        }

        if (refInfo.SearchIn == 'O')
        {
            return entry
                .Database.FindAllEntries(e => e.Attributes.ContainsValue(refInfo.SearchValue))
                .FirstOrDefault();
        }

        string? fieldKey = FieldReference.FieldCodeToKey(refInfo.SearchIn);
        if (fieldKey is null)
        {
            return null;
        }

        return entry
            .Database.FindAllEntries(e => e.Attributes.Get(fieldKey) == refInfo.SearchValue)
            .FirstOrDefault();
    }

    // ── Type classification ──────────────────────────────────────────────────

    private static PlaceholderType ClassifyPlaceholder(string placeholder)
    {
        if (!placeholder.StartsWith('{') || !placeholder.EndsWith('}'))
        {
            return PlaceholderType.NotPlaceholder;
        }

        if (placeholder.StartsWith("{S:", StringComparison.Ordinal))
        {
            return PlaceholderType.CustomAttribute;
        }
        if (placeholder.StartsWith("{REF:", StringComparison.OrdinalIgnoreCase))
        {
            return PlaceholderType.Reference;
        }

        // {T-CONV:…} and {T-REPLACE-RX:…} — defer
        if (placeholder.StartsWith("{T-CONV:", StringComparison.OrdinalIgnoreCase))
        {
            return PlaceholderType.Unknown;
        }
        if (placeholder.StartsWith("{T-REPLACE-RX:", StringComparison.OrdinalIgnoreCase))
        {
            return PlaceholderType.Unknown;
        }

        return s_placeholderMap.GetValueOrDefault(
            placeholder.ToUpperInvariant(),
            PlaceholderType.Unknown
        );
    }

    private static readonly Dictionary<string, PlaceholderType> s_placeholderMap = new()
    {
        ["{TITLE}"] = PlaceholderType.Title,
        ["{USERNAME}"] = PlaceholderType.UserName,
        ["{PASSWORD}"] = PlaceholderType.Password,
        ["{NOTES}"] = PlaceholderType.Notes,
        ["{TOTP}"] = PlaceholderType.Totp,
        ["{TIMEOTP}"] = PlaceholderType.Totp,
        ["{URL}"] = PlaceholderType.Url,
        ["{UUID}"] = PlaceholderType.Uuid,
        ["{URL:RMVSCM}"] = PlaceholderType.UrlWithoutScheme,
        ["{URL:WITHOUTSCHEME}"] = PlaceholderType.UrlWithoutScheme,
        ["{URL:SCM}"] = PlaceholderType.UrlScheme,
        ["{URL:SCHEME}"] = PlaceholderType.UrlScheme,
        ["{URL:HOST}"] = PlaceholderType.UrlHost,
        ["{URL:PORT}"] = PlaceholderType.UrlPort,
        ["{URL:PATH}"] = PlaceholderType.UrlPath,
        ["{URL:QUERY}"] = PlaceholderType.UrlQuery,
        ["{URL:FRAGMENT}"] = PlaceholderType.UrlFragment,
        ["{URL:USERINFO}"] = PlaceholderType.UrlUserInfo,
        ["{URL:USERNAME}"] = PlaceholderType.UrlUserName,
        ["{URL:PASSWORD}"] = PlaceholderType.UrlPassword,
        ["{DT_SIMPLE}"] = PlaceholderType.DateTimeSimple,
        ["{DT_YEAR}"] = PlaceholderType.DateTimeYear,
        ["{DT_MONTH}"] = PlaceholderType.DateTimeMonth,
        ["{DT_DAY}"] = PlaceholderType.DateTimeDay,
        ["{DT_HOUR}"] = PlaceholderType.DateTimeHour,
        ["{DT_MINUTE}"] = PlaceholderType.DateTimeMinute,
        ["{DT_SECOND}"] = PlaceholderType.DateTimeSecond,
        ["{DT_UTC_SIMPLE}"] = PlaceholderType.DateTimeUtcSimple,
        ["{DT_UTC_YEAR}"] = PlaceholderType.DateTimeUtcYear,
        ["{DT_UTC_MONTH}"] = PlaceholderType.DateTimeUtcMonth,
        ["{DT_UTC_DAY}"] = PlaceholderType.DateTimeUtcDay,
        ["{DT_UTC_HOUR}"] = PlaceholderType.DateTimeUtcHour,
        ["{DT_UTC_MINUTE}"] = PlaceholderType.DateTimeUtcMinute,
        ["{DT_UTC_SECOND}"] = PlaceholderType.DateTimeUtcSecond,
        ["{DB_DIR}"] = PlaceholderType.DbDir,
    };

    // ── URL decomposition ────────────────────────────────────────────────────

    private static string ResolveUrlPlaceholder(string url, PlaceholderType type)
    {
        if (string.IsNullOrEmpty(url))
        {
            return "";
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return "";
        }

        return type switch
        {
            PlaceholderType.UrlWithoutScheme => uri.GetComponents(
                UriComponents.AbsoluteUri & ~UriComponents.Scheme,
                UriFormat.Unescaped
            ),
            PlaceholderType.UrlScheme => uri.Scheme,
            PlaceholderType.UrlHost => uri.Host,
            PlaceholderType.UrlPort => uri.IsDefaultPort ? "" : uri.Port.ToString(),
            PlaceholderType.UrlPath => uri.AbsolutePath,
            PlaceholderType.UrlQuery => uri.Query.TrimStart('?'),
            PlaceholderType.UrlFragment => uri.Fragment.TrimStart('#'),
            PlaceholderType.UrlUserInfo => uri.UserInfo,
            PlaceholderType.UrlUserName => GetUrlUserName(uri),
            PlaceholderType.UrlPassword => GetUrlPassword(uri),
            _ => "",
        };
    }

    private static string GetUrlUserName(Uri uri)
    {
        string info = uri.UserInfo;
        int colon = info.IndexOf(':');
        return colon >= 0 ? info[..colon] : info;
    }

    private static string GetUrlPassword(Uri uri)
    {
        string info = uri.UserInfo;
        int colon = info.IndexOf(':');
        return colon >= 0 ? info[(colon + 1)..] : "";
    }

    // ── DateTime formatting ───────────────────────────────────────────────────

    private static string ResolveDateTimePlaceholder(PlaceholderType type)
    {
        DateTime now = DateTime.Now;
        DateTime utc = DateTime.UtcNow;

        return type switch
        {
            PlaceholderType.DateTimeSimple => now.ToString("yyyyMMddHHmmss"),
            PlaceholderType.DateTimeYear => now.ToString("yyyy"),
            PlaceholderType.DateTimeMonth => now.ToString("MM"),
            PlaceholderType.DateTimeDay => now.ToString("dd"),
            PlaceholderType.DateTimeHour => now.ToString("HH"),
            PlaceholderType.DateTimeMinute => now.ToString("mm"),
            PlaceholderType.DateTimeSecond => now.ToString("ss"),
            PlaceholderType.DateTimeUtcSimple => utc.ToString("yyyyMMddHHmmss"),
            PlaceholderType.DateTimeUtcYear => utc.ToString("yyyy"),
            PlaceholderType.DateTimeUtcMonth => utc.ToString("MM"),
            PlaceholderType.DateTimeUtcDay => utc.ToString("dd"),
            PlaceholderType.DateTimeUtcHour => utc.ToString("HH"),
            PlaceholderType.DateTimeUtcMinute => utc.ToString("mm"),
            PlaceholderType.DateTimeUtcSecond => utc.ToString("ss"),
            _ => "",
        };
    }

    // ── Placeholder type ─────────────────────────────────────────────────────
}

/// <summary>
/// Classification of a <c>{…}</c> placeholder token. Mirrors KeePassXC's
/// <c>Entry::PlaceholderType</c>.
/// </summary>
public enum PlaceholderType
{
    Unknown,
    NotPlaceholder,
    Title,
    UserName,
    Password,
    Notes,
    Url,
    Uuid,
    Totp,
    DbDir,
    UrlWithoutScheme,
    UrlScheme,
    UrlHost,
    UrlPort,
    UrlPath,
    UrlQuery,
    UrlFragment,
    UrlUserInfo,
    UrlUserName,
    UrlPassword,
    DateTimeSimple,
    DateTimeYear,
    DateTimeMonth,
    DateTimeDay,
    DateTimeHour,
    DateTimeMinute,
    DateTimeSecond,
    DateTimeUtcSimple,
    DateTimeUtcYear,
    DateTimeUtcMonth,
    DateTimeUtcDay,
    DateTimeUtcHour,
    DateTimeUtcMinute,
    DateTimeUtcSecond,
    CustomAttribute,
    Reference,
}
