namespace LibKdbx.Tests;

public class PlaceholderResolverTests
{
    // ── Helper ───────────────────────────────────────────────────────────────

    private static Entry CreateEntry()
    {
        Entry entry = new()
        {
            Title = "MyEntry",
            UserName = "alice",
            Password = "secret123",
            Url = "https://example.com:8080/path?q=1#sec",
            Notes = "some notes",
        };
        return entry;
    }

    private static string Resolve(Entry entry, string input) =>
        PlaceholderResolver.Resolve(entry, input);

    // ── Basic placeholders ───────────────────────────────────────────────────

    [Theory]
    [InlineData("{TITLE}", "MyEntry")]
    [InlineData("{USERNAME}", "alice")]
    [InlineData("{PASSWORD}", "secret123")]
    [InlineData("{NOTES}", "some notes")]
    public void Basic_Placeholder(string input, string expected)
    {
        Entry entry = CreateEntry();
        Resolve(entry, input).ShouldBe(expected);
    }

    [Fact]
    public void Url()
    {
        Entry entry = CreateEntry();
        Resolve(entry, "{URL}").ShouldBe("https://example.com:8080/path?q=1#sec");
    }

    [Fact]
    public void Uuid()
    {
        Entry entry = CreateEntry();
        Resolve(entry, "{UUID}").ShouldBe(entry.Uuid.ToString("N"));
    }

    // ── Custom attribute ─────────────────────────────────────────────────────

    [Fact]
    public void CustomAttribute()
    {
        Entry entry = CreateEntry();
        entry.Attributes.Set("Server", "prod-01");
        Resolve(entry, "{S:Server}").ShouldBe("prod-01");
    }

    [Fact]
    public void CustomAttribute_Missing_Returns_Empty()
    {
        Entry entry = CreateEntry();
        Resolve(entry, "{S:MissingKey}").ShouldBe("");
    }

    // ── DB_DIR ───────────────────────────────────────────────────────────────

    [Fact]
    public void DbDir_With_Database()
    {
        using Database db = Database.Create("pw");
        Entry entry = new() { Title = "E", UserName = "u" };
        db.RootGroup!.AddEntry(entry);
        string tempPath = Path.GetTempFileName();
        try
        {
            db.SaveAs(tempPath);
            Resolve(entry, "{DB_DIR}").ShouldBe(Path.GetDirectoryName(tempPath));
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void DbDir_No_Database_Returns_Empty()
    {
        Entry entry = CreateEntry();
        Resolve(entry, "{DB_DIR}").ShouldBe("");
    }

    // ── TOTP ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Totp_Returns_Empty_For_Now()
    {
        Entry entry = CreateEntry();
        entry.Attributes.Set("TOTP Seed", "JBSWY3DPEHPK3PXP");
        // TOTP generation deferred — returns empty for now
        Resolve(entry, "{TOTP}").ShouldBe("");
    }

    // ── URL decomposition ────────────────────────────────────────────────────

    [Fact]
    public void UrlWithoutScheme()
    {
        Entry entry = CreateEntry();
        string result = Resolve(entry, "{URL:RMVSCM}");
        result.ShouldContain("example.com");
        result.ShouldNotContain("https://");
    }

    [Fact]
    public void UrlScheme()
    {
        Entry entry = CreateEntry();
        Resolve(entry, "{URL:SCM}").ShouldBe("https");
        Resolve(entry, "{URL:SCHEME}").ShouldBe("https");
    }

    [Fact]
    public void UrlHost()
    {
        Entry entry = CreateEntry();
        Resolve(entry, "{URL:HOST}").ShouldBe("example.com");
    }

    [Fact]
    public void UrlPort()
    {
        Entry entry = CreateEntry();
        Resolve(entry, "{URL:PORT}").ShouldBe("8080");
    }

    [Fact]
    public void UrlPort_Default_Empty()
    {
        Entry entry = CreateEntry();
        entry.Url = "https://example.com";
        Resolve(entry, "{URL:PORT}").ShouldBe("");
    }

    [Fact]
    public void UrlPath()
    {
        Entry entry = CreateEntry();
        Resolve(entry, "{URL:PATH}").ShouldBe("/path");
    }

    [Fact]
    public void UrlQuery()
    {
        Entry entry = CreateEntry();
        Resolve(entry, "{URL:QUERY}").ShouldBe("q=1");
    }

    [Fact]
    public void UrlFragment()
    {
        Entry entry = CreateEntry();
        Resolve(entry, "{URL:FRAGMENT}").ShouldBe("sec");
    }

    [Fact]
    public void UrlUserInfo()
    {
        Entry entry = CreateEntry();
        entry.Url = "https://user:pass@example.com";
        Resolve(entry, "{URL:USERINFO}").ShouldBe("user:pass");
    }

    [Fact]
    public void UrlUserName()
    {
        Entry entry = CreateEntry();
        entry.Url = "https://user:pass@example.com";
        Resolve(entry, "{URL:USERNAME}").ShouldBe("user");
    }

    [Fact]
    public void UrlPassword()
    {
        Entry entry = CreateEntry();
        entry.Url = "https://user:pass@example.com";
        Resolve(entry, "{URL:PASSWORD}").ShouldBe("pass");
    }

    [Fact]
    public void UrlPlaceholder_Empty_Url()
    {
        Entry entry = CreateEntry();
        entry.Url = "";
        Resolve(entry, "{URL:HOST}").ShouldBe("");
    }

    [Fact]
    public void UrlPlaceholder_Invalid_Url()
    {
        Entry entry = CreateEntry();
        entry.Url = "not-a-valid-url";
        Resolve(entry, "{URL:HOST}").ShouldBe("");
    }

    // ── DateTime ─────────────────────────────────────────────────────────────

    [Fact]
    public void DateTimeSimple()
    {
        Entry entry = CreateEntry();
        string result = Resolve(entry, "{DT_SIMPLE}");
        result.Length.ShouldBe(14); // yyyyMMddHHmmss
        // Verify it's all digits
        result.All(char.IsDigit).ShouldBeTrue();
    }

    [Fact]
    public void DateTimeYear()
    {
        Entry entry = CreateEntry();
        string result = Resolve(entry, "{DT_YEAR}");
        result.Length.ShouldBe(4);
        result.ShouldBe(DateTime.Now.Year.ToString());
    }

    [Fact]
    public void DateTimeMonth()
    {
        Entry entry = CreateEntry();
        string result = Resolve(entry, "{DT_MONTH}");
        result.Length.ShouldBe(2);
        int.Parse(result).ShouldBeInRange(1, 12);
    }

    [Fact]
    public void DateTimeDay()
    {
        Entry entry = CreateEntry();
        string result = Resolve(entry, "{DT_DAY}");
        result.Length.ShouldBe(2);
        int.Parse(result).ShouldBeInRange(1, 31);
    }

    [Fact]
    public void DateTimeUtcSimple()
    {
        Entry entry = CreateEntry();
        string result = Resolve(entry, "{DT_UTC_SIMPLE}");
        result.Length.ShouldBe(14);
    }

    [Fact]
    public void DateTimeUtcYear()
    {
        Entry entry = CreateEntry();
        string result = Resolve(entry, "{DT_UTC_YEAR}");
        result.ShouldBe(DateTime.UtcNow.Year.ToString());
    }

    // ── Nested / recursive resolution ────────────────────────────────────────

    [Fact]
    public void Nested_Placeholder_In_Custom_Attribute()
    {
        Entry entry = CreateEntry();
        entry.Attributes.Set("DisplayName", "User: {USERNAME}");
        Resolve(entry, "{S:DisplayName}").ShouldBe("User: alice");
    }

    [Fact]
    public void Nested_Placeholder_In_Title()
    {
        Entry entry = CreateEntry();
        entry.Title = "App-{S:Env}";
        entry.Attributes.Set("Env", "production");
        Resolve(entry, "{TITLE}").ShouldBe("App-production");
    }

    [Fact]
    public void Unknown_Placeholder_Preserved()
    {
        Entry entry = CreateEntry();
        Resolve(entry, "{UNKNOWN}").ShouldBe("{UNKNOWN}");
    }

    [Fact]
    public void Unknown_Placeholder_With_Inner_Resolution()
    {
        Entry entry = CreateEntry();
        entry.Attributes.Set("Env", "prod");
        // {CUSTOM:{S:Env}} — unknown outer, inner resolves
        Resolve(entry, "{CUSTOM:{S:Env}}").ShouldBe("{CUSTOM:prod}");
    }

    [Fact]
    public void Plain_Text_Unchanged()
    {
        Entry entry = CreateEntry();
        Resolve(entry, "plain text no braces").ShouldBe("plain text no braces");
    }

    [Fact]
    public void Mixed_Placeholders_And_Text()
    {
        Entry entry = CreateEntry();
        Resolve(entry, "User {USERNAME} logged into {URL}")
            .ShouldBe("User alice logged into https://example.com:8080/path?q=1#sec");
    }

    [Fact]
    public void Escaped_Braces_Literal()
    {
        Entry entry = CreateEntry();
        // The escaped string comes from input like "\\{TITLE\\}"
        // which in C# string literal is "\\{TITLE\\}"
        // KeePassXC stores escaped as \{TITLE\}
        // Our resolver handles: input starts with "\{" and ends with "\}"
        // Test with a standalone escaped token:
        Resolve(entry, @"\{TITLE\}").ShouldBe("{TITLE}");
    }

    // ── Depth limit ──────────────────────────────────────────────────────────

    [Fact]
    public void Depth_Limit_Prevents_Infinite_Loop()
    {
        Entry entry = CreateEntry();
        entry.Attributes.Set("Loop", "{S:Loop}"); // circular reference
        string result = Resolve(entry, "{S:Loop}");
        // Should return something without stack overflow
        result.Length.ShouldBeGreaterThan(0);
    }

    // ── Field references ─────────────────────────────────────────────────────

    [Fact]
    public void Reference_Title_To_Title()
    {
        using Database db = Database.Create("pw");
        Entry target = new()
        {
            Title = "Target",
            UserName = "bob",
            Password = "pw",
        };
        db.RootGroup!.AddEntry(target);

        Entry source = new()
        {
            Title = "Source",
            UserName = "alice",
            Password = "refpw",
        };
        db.RootGroup.AddEntry(source);
        source.Notes = "{REF:N@T:Target}";

        Resolve(source, "{NOTES}").ShouldBe(target.Notes);
    }

    [Fact]
    public void Reference_Uuid_WantedField()
    {
        using Database db = Database.Create("pw");
        Entry target = new()
        {
            Uuid = Guid.NewGuid(),
            Title = "Target",
            UserName = "bob",
        };
        db.RootGroup!.AddEntry(target);

        Entry source = new()
        {
            Title = "Source",
            UserName = "alice",
            Notes = $"{{REF:I@T:Target}}",
        };
        db.RootGroup.AddEntry(source);

        Resolve(source, "{NOTES}").ShouldBe(target.Uuid.ToString("N"));
    }

    [Fact]
    public void Reference_No_Database_Returns_Token()
    {
        Entry entry = CreateEntry();
        entry.Notes = "{REF:N@T:Target}";
        // Without a database, REF can't be resolved and passes through as-is
        Resolve(entry, "{NOTES}").ShouldBe("{REF:N@T:Target}");
    }

    // ── AutoType sequence examples ───────────────────────────────────────────

    [Fact]
    public void AutoType_Sequence_Username_Tab_Password()
    {
        Entry entry = CreateEntry();
        Resolve(entry, "{USERNAME}{TAB}{PASSWORD}{ENTER}").ShouldBe("alice{TAB}secret123{ENTER}");
    }

    [Fact]
    public void Entry_ResolvePlaceholder_Convenience()
    {
        Entry entry = CreateEntry();
        entry.ResolvePlaceholder("{TITLE}").ShouldBe("MyEntry");
    }

    // ── Classify ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("{TITLE}", PlaceholderType.Title)]
    [InlineData("{USERNAME}", PlaceholderType.UserName)]
    [InlineData("{PASSWORD}", PlaceholderType.Password)]
    [InlineData("{URL}", PlaceholderType.Url)]
    [InlineData("{NOTES}", PlaceholderType.Notes)]
    [InlineData("{UUID}", PlaceholderType.Uuid)]
    [InlineData("{TOTP}", PlaceholderType.Totp)]
    [InlineData("{TIMEOTP}", PlaceholderType.Totp)]
    [InlineData("{DB_DIR}", PlaceholderType.DbDir)]
    [InlineData("{S:attr}", PlaceholderType.CustomAttribute)]
    [InlineData("{REF:T@I:abc123}", PlaceholderType.Reference)]
    [InlineData("{URL:HOST}", PlaceholderType.UrlHost)]
    [InlineData("{URL:PORT}", PlaceholderType.UrlPort)]
    [InlineData("{URL:PATH}", PlaceholderType.UrlPath)]
    [InlineData("{URL:QUERY}", PlaceholderType.UrlQuery)]
    [InlineData("{URL:FRAGMENT}", PlaceholderType.UrlFragment)]
    [InlineData("{URL:SCM}", PlaceholderType.UrlScheme)]
    [InlineData("{URL:SCHEME}", PlaceholderType.UrlScheme)]
    [InlineData("{URL:USERINFO}", PlaceholderType.UrlUserInfo)]
    [InlineData("{URL:USERNAME}", PlaceholderType.UrlUserName)]
    [InlineData("{URL:PASSWORD}", PlaceholderType.UrlPassword)]
    [InlineData("{DT_SIMPLE}", PlaceholderType.DateTimeSimple)]
    [InlineData("{DT_YEAR}", PlaceholderType.DateTimeYear)]
    [InlineData("{DT_UTC_SIMPLE}", PlaceholderType.DateTimeUtcSimple)]
    [InlineData("{NOT_A_REAL_PLACEHOLDER}", PlaceholderType.Unknown)]
    [InlineData("plain text", PlaceholderType.NotPlaceholder)]
    public void Classify(string input, PlaceholderType expected)
    {
        PlaceholderResolver.Classify(input).ShouldBe(expected);
    }

    [Fact]
    public void Classify_UrlWithoutScheme_Both_Aliases()
    {
        PlaceholderResolver.Classify("{URL:RMVSCM}").ShouldBe(PlaceholderType.UrlWithoutScheme);
        PlaceholderResolver
            .Classify("{URL:WITHOUTSCHEME}")
            .ShouldBe(PlaceholderType.UrlWithoutScheme);
    }

    [Fact]
    public void Classify_Case_Insensitive()
    {
        PlaceholderResolver.Classify("{title}").ShouldBe(PlaceholderType.Title);
        PlaceholderResolver.Classify("{url:host}").ShouldBe(PlaceholderType.UrlHost);
    }
}
