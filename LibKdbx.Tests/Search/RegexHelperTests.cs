using System.Text.RegularExpressions;

namespace LibKdbx.Tests;

public class RegexHelperTests
{
    // ── ConvertToRegex ──────────────────────────────────────────────────────

    [Fact]
    public void ConvertToRegex_PlainText_Default()
    {
        Regex r = RegexHelper.ConvertToRegex("hello");
        r.IsMatch("Hello there").ShouldBeTrue();
    }

    [Fact]
    public void ConvertToRegex_PlainText_CaseInsensitive_By_Default()
    {
        Regex r = RegexHelper.ConvertToRegex("HELLO");
        r.IsMatch("hello").ShouldBeTrue();
    }

    [Fact]
    public void ConvertToRegex_WildcardStar()
    {
        Regex r = RegexHelper.ConvertToRegex("he*lo", RegexConvertOptions.WildcardAll);
        r.IsMatch("heXXXXXlo").ShouldBeTrue();
        r.IsMatch("helo").ShouldBeTrue(); // * matches zero chars
    }

    [Fact]
    public void ConvertToRegex_WildcardQuestion()
    {
        Regex r = RegexHelper.ConvertToRegex("user?", RegexConvertOptions.WildcardAll);
        r.IsMatch("users").ShouldBeTrue();
        r.IsMatch("user").ShouldBeFalse();
    }

    [Fact]
    public void ConvertToRegex_WildcardPipe()
    {
        Regex r = RegexHelper.ConvertToRegex("foo|bar", RegexConvertOptions.WildcardAll);
        r.IsMatch("foo").ShouldBeTrue();
        r.IsMatch("bar").ShouldBeTrue();
        r.IsMatch("baz").ShouldBeFalse();
    }

    [Fact]
    public void ConvertToRegex_ExactMatch()
    {
        Regex r = RegexHelper.ConvertToRegex("hello", RegexConvertOptions.ExactMatch);
        r.IsMatch("hello").ShouldBeTrue();
        r.IsMatch("hello world").ShouldBeFalse();
    }

    [Fact]
    public void ConvertToRegex_ExactMatch_Combined_With_Wildcard()
    {
        Regex r = RegexHelper.ConvertToRegex(
            "hello*",
            RegexConvertOptions.ExactMatch | RegexConvertOptions.WildcardAll
        );
        r.IsMatch("hello").ShouldBeTrue();
        r.IsMatch("hello world").ShouldBeTrue();
        r.IsMatch("XhelloX").ShouldBeFalse(); // exact, must match whole string
    }

    [Fact]
    public void ConvertToRegex_CaseSensitive()
    {
        Regex r = RegexHelper.ConvertToRegex("Hello", RegexConvertOptions.CaseSensitive);
        r.IsMatch("Hello").ShouldBeTrue();
        r.IsMatch("hello").ShouldBeFalse();
    }

    [Fact]
    public void ConvertToRegex_EscapesMetaChars_Before_Wildcard_Conversion()
    {
        // Without WildcardAll: raw regex → "." matches any char
        Regex raw = RegexHelper.ConvertToRegex("a.b", RegexConvertOptions.None);
        raw.IsMatch("axb").ShouldBeTrue();

        // With WildcardAll: "." is escaped to "\." → literal period
        Regex wild = RegexHelper.ConvertToRegex("a.b", RegexConvertOptions.WildcardAll);
        wild.IsMatch("a.b").ShouldBeTrue();
        wild.IsMatch("axb").ShouldBeFalse();
    }

    [Fact]
    public void ConvertToRegex_EscapesSquareBrackets()
    {
        Regex r = RegexHelper.ConvertToRegex("[test]", RegexConvertOptions.WildcardAll);
        r.IsMatch("[test]").ShouldBeTrue();
        // Without escaping, "[test]" would match any of t,e,s,t chars
        r.IsMatch("t").ShouldBeFalse();
    }

    [Fact]
    public void ConvertToRegex_RawRegex_Mode_NoEscaping()
    {
        // None = raw regex. "." matches any char.
        Regex r = RegexHelper.ConvertToRegex("t.st", RegexConvertOptions.None);
        r.IsMatch("test").ShouldBeTrue();
        r.IsMatch("tast").ShouldBeTrue();
    }

    [Fact]
    public void ConvertToRegex_RawRegex_With_WildcardAll_And_StarModifier()
    {
        // KeePassXC: when "*" modifier is specified, WildcardAll is NOT set,
        // so the input is treated as raw regex. This test verifies that behavior.
        Regex r = RegexHelper.ConvertToRegex("te.*st", RegexConvertOptions.None);
        r.IsMatch("teABCst").ShouldBeTrue();
    }

    [Fact]
    public void ConvertToRegex_Empty_String()
    {
        Regex r = RegexHelper.ConvertToRegex("", RegexConvertOptions.WildcardAll);
        r.IsMatch("").ShouldBeTrue();
        r.IsMatch("anything").ShouldBeTrue(); // empty pattern matches everywhere
    }

    [Fact]
    public void ConvertToRegex_Empty_String_ExactMatch()
    {
        Regex r = RegexHelper.ConvertToRegex("", RegexConvertOptions.ExactMatch);
        r.IsMatch("").ShouldBeTrue();
        r.IsMatch("x").ShouldBeFalse();
    }

    // ── EscapeRegex ─────────────────────────────────────────────────────────

    [Fact]
    public void EscapeRegex_Alphanumeric_Unchanged()
    {
        string result = RegexHelper.EscapeRegex("helloWORLD123");
        result.ShouldBe("helloWORLD123");
    }

    [Fact]
    public void EscapeRegex_Dot_Escaped()
    {
        string result = RegexHelper.EscapeRegex("file.txt");
        result.ShouldBe(@"file\.txt");
    }

    [Fact]
    public void EscapeRegex_Star_Escaped()
    {
        string result = RegexHelper.EscapeRegex("a*b");
        result.ShouldBe(@"a\*b");
    }

    [Fact]
    public void EscapeRegex_Question_Escaped()
    {
        string result = RegexHelper.EscapeRegex("a?b");
        result.ShouldBe(@"a\?b");
    }

    [Fact]
    public void EscapeRegex_Pipe_Escaped()
    {
        string result = RegexHelper.EscapeRegex("a|b");
        result.ShouldBe(@"a\|b");
    }

    [Fact]
    public void EscapeRegex_Brackets_Escaped()
    {
        string result = RegexHelper.EscapeRegex("[test]");
        result.ShouldBe(@"\[test\]");
    }

    [Fact]
    public void EscapeRegex_Parentheses_Escaped()
    {
        string result = RegexHelper.EscapeRegex("(group)");
        result.ShouldBe(@"\(group\)");
    }

    [Fact]
    public void EscapeRegex_Backslash_Escaped()
    {
        string result = RegexHelper.EscapeRegex(@"a\b");
        result.ShouldBe(@"a\\b");
    }

    [Fact]
    public void EscapeRegex_Dollar_Caret_Escaped()
    {
        string result = RegexHelper.EscapeRegex("^start$");
        result.ShouldBe(@"\^start\$");
    }

    [Fact]
    public void EscapeRegex_Plus_Escaped()
    {
        string result = RegexHelper.EscapeRegex("a+b");
        result.ShouldBe(@"a\+b");
    }

    [Fact]
    public void EscapeRegex_Underscore_Preserved()
    {
        string result = RegexHelper.EscapeRegex("my_var_name");
        result.ShouldBe("my_var_name");
    }

    [Fact]
    public void EscapeRegex_Hyphen_Escaped()
    {
        string result = RegexHelper.EscapeRegex("my-var");
        result.ShouldBe(@"my\-var");
    }

    [Fact]
    public void EscapeRegex_Space_Escaped()
    {
        string result = RegexHelper.EscapeRegex("hello world");
        result.ShouldBe(@"hello\ world");
    }

    [Fact]
    public void EscapeRegex_Empty_String()
    {
        string result = RegexHelper.EscapeRegex("");
        result.ShouldBe("");
    }
}
