using System.Text.RegularExpressions;

namespace LibKdbx;

/// <summary>
/// Options controlling how a search term is converted to a Regex pattern.
/// </summary>
[Flags]
public enum RegexConvertOptions
{
    /// <summary>Raw regex, no escaping or wildcard conversion.</summary>
    None = 0,

    /// <summary>
    /// Escape regex metacharacters, then convert *, ?, | to wildcards.
    /// Combines escape + wildcard conversion in one flag.
    /// </summary>
    WildcardAll = 1,

    /// <summary>Wrap the pattern with ^(?:…)$ for exact match.</summary>
    ExactMatch = 2,

    /// <summary>Case-sensitive matching. Default is case-insensitive.</summary>
    CaseSensitive = 4,
}

/// <summary>
/// Converts KeePassXC-style search terms to .NET <see cref="Regex"/> patterns.
/// Ported from KeePassXC's <c>Tools::convertToRegex</c>.
/// </summary>
public static partial class RegexHelper
{
    /// <summary>
    /// Converts <paramref name="pattern"/> to a <see cref="Regex"/>.
    /// By default, the pattern is treated as a raw regular expression (no escaping,
    /// case-insensitive). Use <see cref="RegexConvertOptions.WildcardAll"/> to
    /// enable wildcard support with automatic metacharacter escaping.
    /// </summary>
    public static Regex ConvertToRegex(
        string pattern,
        RegexConvertOptions options = RegexConvertOptions.None
    )
    {
        string processed = pattern;

        if ((options & RegexConvertOptions.WildcardAll) != 0)
        {
            processed = EscapeRegex(processed);
            processed = processed.Replace("\\*", ".*");
            processed = processed.Replace("\\?", ".");
            processed = processed.Replace("\\|", "|");
        }

        if ((options & RegexConvertOptions.ExactMatch) != 0)
        {
            processed = "^(?:" + processed + ")$";
        }

        RegexOptions regexOptions = RegexOptions.None;
        if ((options & RegexConvertOptions.CaseSensitive) == 0)
        {
            regexOptions |= RegexOptions.IgnoreCase;
        }

        return new Regex(processed, regexOptions);
    }

    /// <summary>
    /// Escapes special regex characters in <paramref name="input"/>, preserving
    /// only [a-zA-Z0-9_]. Ported from KeePassXC's <c>Tools::escapeRegex</c>.
    /// </summary>
    public static string EscapeRegex(string input)
    {
        return EscapeRegexImpl()
            .Replace(
                input,
                m =>
                {
                    char c = m.Value[0];
                    return "\\" + c;
                }
            );
    }

    [GeneratedRegex(@"[^a-zA-Z0-9_]")]
    private static partial Regex EscapeRegexImpl();
}
