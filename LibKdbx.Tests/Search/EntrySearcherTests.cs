namespace LibKdbx.Tests;

public class EntrySearcherTests
{
    // ── Helper ───────────────────────────────────────────────────────────────

    private static (Group root, Entry entry) CreateDbWithEntry(
        string title = "TestEntry",
        string? password = null
    )
    {
        Group root = new() { Name = "Root" };
        Entry entry = new()
        {
            Title = title,
            UserName = "alice",
            Password = password ?? "secret123",
            Url = "https://example.com",
            Notes = "some notes here",
            Tags = "",
        };
        root.AddEntry(entry);
        return (root, entry);
    }

    private static EntrySearcher CreateSearcher(
        bool caseSensitive = false,
        bool skipProtected = false
    ) => new(caseSensitive, skipProtected);

    // ── Empty / trivial ──────────────────────────────────────────────────────

    [Fact]
    public void Empty_Terms_Matches_All()
    {
        (Group root, _) = CreateDbWithEntry("A");
        Entry e2 = new() { Title = "B" };
        root.AddEntry(e2);

        List<Entry> results = CreateSearcher().Search("", root);
        results.Count.ShouldBe(2);
    }

    [Fact]
    public void SkipProtected_With_Empty_Terms_Rejects_Everything()
    {
        (Group root, _) = CreateDbWithEntry("A");
        Entry e2 = new() { Title = "B" };
        root.AddEntry(e2);

        EntrySearcher searcher = CreateSearcher(skipProtected: true);
        List<Entry> results = searcher.Search("", root);
        results.Count.ShouldBe(0);
    }

    [Fact]
    public void SkipProtected_With_Term_Matches_NonProtected()
    {
        (Group root, _) = CreateDbWithEntry("A");
        Entry e2 = new() { Title = "B", Password = "secret" };
        root.AddEntry(e2);

        EntrySearcher searcher = CreateSearcher(skipProtected: true);
        // Searching password while skipProtected — password term should be skipped
        List<Entry> results = searcher.Search("title:B", root);
        results.Count.ShouldBe(1);
        results[0].Title.ShouldBe("B");
    }

    // ── Title search ─────────────────────────────────────────────────────────

    [Fact]
    public void Title_Exact_Match()
    {
        (Group root, _) = CreateDbWithEntry("GitHub");
        Entry e2 = new() { Title = "GitLab" };
        root.AddEntry(e2);

        List<Entry> results = CreateSearcher().Search("title:GitHub", root);
        results.Count.ShouldBe(1);
        results[0].Title.ShouldBe("GitHub");
    }

    [Fact]
    public void Title_Case_Insensitive_By_Default()
    {
        (Group root, _) = CreateDbWithEntry("GitHub");
        List<Entry> results = CreateSearcher().Search("title:github", root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void Title_Case_Sensitive()
    {
        (Group root, _) = CreateDbWithEntry("GitHub");
        List<Entry> results = CreateSearcher(caseSensitive: true).Search("title:GitHub", root);
        results.Count.ShouldBe(1);

        List<Entry> results2 = CreateSearcher(caseSensitive: true).Search("title:github", root);
        results2.Count.ShouldBe(0);
    }

    [Fact]
    public void Title_Wildcard_Star()
    {
        (Group root, _) = CreateDbWithEntry("GitHub");
        Entry e2 = new() { Title = "GitLab" };
        root.AddEntry(e2);

        List<Entry> results = CreateSearcher().Search("title:Git*", root);
        results.Count.ShouldBe(2);
    }

    [Fact]
    public void Title_Wildcard_Question()
    {
        (Group root, _) = CreateDbWithEntry("user1");
        Entry e2 = new() { Title = "user2" };
        root.AddEntry(e2);

        List<Entry> results = CreateSearcher().Search("title:user?", root);
        results.Count.ShouldBe(2);
    }

    [Fact]
    public void Title_Exact_Modifier()
    {
        (Group root, _) = CreateDbWithEntry("GitHub");
        Entry e2 = new() { Title = "GitHub Enterprise" };
        root.AddEntry(e2);

        List<Entry> results = CreateSearcher().Search("+title:GitHub", root);
        results.Count.ShouldBe(1);
        results[0].Title.ShouldBe("GitHub");
    }

    // ── Broad search (Undefined field) ───────────────────────────────────────

    [Theory]
    [InlineData("alice")] // matches username
    [InlineData("example.com")] // matches url
    [InlineData("some notes")] // matches notes
    public void Undefined_Matches_Defaults(string query)
    {
        (Group root, _) = CreateDbWithEntry();
        List<Entry> results = CreateSearcher().Search(query, root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void Undefined_Matches_Custom_Title()
    {
        (Group root, _) = CreateDbWithEntry("MySecretApp");
        List<Entry> results = CreateSearcher().Search("MySecret", root);
        results.Count.ShouldBe(1);
    }

    // ── Field-specific ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("username:alice")]
    [InlineData("password:secret123")]
    [InlineData("pw:hunter2", "TestEntry", "hunter2")]
    [InlineData("url:https://example.com")]
    [InlineData("notes:some")]
    public void Field_Match(string query, string? title = null, string? password = null)
    {
        (Group root, _) = CreateDbWithEntry(title ?? "TestEntry", password);
        List<Entry> results = CreateSearcher().Search(query, root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void Password_Skipped_When_Protected()
    {
        (Group root, Entry entry) = CreateDbWithEntry(password: "mysecret");
        entry.Attributes.Set("Password", "mysecret", protect: true);

        EntrySearcher searcher = CreateSearcher(skipProtected: true);
        List<Entry> results = searcher.Search("password:mysecret", root);
        results.Count.ShouldBe(0);
    }

    [Fact]
    public void Password_Not_Skipped_Without_Flag()
    {
        (Group root, Entry entry) = CreateDbWithEntry(password: "mysecret");
        entry.Attributes.Set("Password", "mysecret", protect: true);

        List<Entry> results = CreateSearcher().Search("password:mysecret", root);
        results.Count.ShouldBe(1);
    }

    // ── Exclude ──────────────────────────────────────────────────────────────

    [Fact]
    public void Exclude_Dash()
    {
        (Group root, _) = CreateDbWithEntry("GitHub");
        Entry e2 = new() { Title = "Personal" };
        root.AddEntry(e2);

        List<Entry> results = CreateSearcher().Search("-title:GitHub", root);
        results.Count.ShouldBe(1);
        results[0].Title.ShouldBe("Personal");
    }

    [Fact]
    public void Exclude_Bang()
    {
        (Group root, _) = CreateDbWithEntry("Keep");
        Entry e2 = new() { Title = "Remove" };
        root.AddEntry(e2);

        List<Entry> results = CreateSearcher().Search("!title:Keep", root);
        results.Count.ShouldBe(1);
        results[0].Title.ShouldBe("Remove");
    }

    [Fact]
    public void Exclude_All_Excluded_Returns_Empty()
    {
        (Group root, _) = CreateDbWithEntry("X");
        List<Entry> results = CreateSearcher().Search("-title:X", root);
        results.Count.ShouldBe(0);
    }

    // ── Multiple terms (AND) ─────────────────────────────────────────────────

    [Fact]
    public void Multiple_Terms_AND()
    {
        (Group root, _) = CreateDbWithEntry("GitHub");
        // Matches both title AND url
        List<Entry> results = CreateSearcher().Search("title:Git url:example.com", root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void Multiple_Terms_AND_Conflict_Returns_Empty()
    {
        (Group root, _) = CreateDbWithEntry("GitHub");
        // Title matches but url doesn't
        List<Entry> results = CreateSearcher().Search("title:Git url:nowhere.com", root);
        results.Count.ShouldBe(0);
    }

    // ── Quoted strings ───────────────────────────────────────────────────────

    [Fact]
    public void Quoted_String_With_Spaces()
    {
        Entry e = new() { Title = "My Special Entry", UserName = "bob" };
        Group root = new();
        root.AddEntry(e);

        List<Entry> results = CreateSearcher().Search("title:\"My Special Entry\"", root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void Quoted_String_With_Escaped_Quote()
    {
        Entry e = new() { Title = "He said \"hello\"", UserName = "bob" };
        Group root = new();
        root.AddEntry(e);

        // Search for literal quote
        List<Entry> results = CreateSearcher().Search("title:\"He said \\\"hello\\\"\"", root);
        results.Count.ShouldBe(1);
    }

    // ── Group search ─────────────────────────────────────────────────────────

    [Fact]
    public void Group_Name_Search()
    {
        Group root = new() { Name = "Root" };
        Group work = new() { Name = "Work" };
        root.AddGroup(work);
        Entry e = new() { Title = "VPN", UserName = "bob" };
        work.AddEntry(e);

        List<Entry> results = CreateSearcher().Search("group:Work", root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void Group_Hierarchy_Search()
    {
        Group root = new() { Name = "Root" };
        Group work = new() { Name = "Work" };
        Group dev = new() { Name = "Dev" };
        root.AddGroup(work);
        work.AddGroup(dev);
        Entry e = new() { Title = "AWS", UserName = "bob" };
        dev.AddEntry(e);

        List<Entry> results = CreateSearcher().Search("group:/Root/Work/Dev", root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void Group_Hierarchy_Wildcard()
    {
        Group root = new() { Name = "Root" };
        Group work = new() { Name = "Work" };
        root.AddGroup(work);
        Entry e = new() { Title = "VPN", UserName = "bob" };
        work.AddEntry(e);

        List<Entry> results = CreateSearcher().Search("group:/*/Work", root);
        results.Count.ShouldBe(1);
    }

    // ── Tag search ───────────────────────────────────────────────────────────

    [Fact]
    public void Tag_Search()
    {
        (Group root, Entry entry) = CreateDbWithEntry();
        entry.Tags = "finance;important;work";

        List<Entry> results = CreateSearcher().Search("tag:finance", root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void Tag_Search_Case_Insensitive()
    {
        (Group root, Entry entry) = CreateDbWithEntry();
        entry.Tags = "IMPORTANT";

        List<Entry> results = CreateSearcher().Search("tag:important", root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void Tag_Search_No_Match()
    {
        (Group root, Entry entry) = CreateDbWithEntry();
        entry.Tags = "work";

        List<Entry> results = CreateSearcher().Search("tag:personal", root);
        results.Count.ShouldBe(0);
    }

    // ── UUID search ──────────────────────────────────────────────────────────

    [Fact]
    public void Uuid_Search()
    {
        Guid uuid = Guid.NewGuid();
        Entry e = new()
        {
            Uuid = uuid,
            Title = "Target",
            UserName = "bob",
        };
        Group root = new();
        root.AddEntry(e);

        string hex = uuid.ToString("N");
        List<Entry> results = CreateSearcher().Search($"uuid:{hex[..8]}*", root);
        results.Count.ShouldBe(1);
    }

    // ── Custom attributes ────────────────────────────────────────────────────

    [Fact]
    public void AttributeKV_Search_Key()
    {
        (Group root, Entry entry) = CreateDbWithEntry();
        entry.Attributes.Set("Server", "prod-01");

        List<Entry> results = CreateSearcher().Search("attribute:server", root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void AttributeKV_Search_Value()
    {
        (Group root, Entry entry) = CreateDbWithEntry();
        entry.Attributes.Set("Server", "prod-db-01");

        List<Entry> results = CreateSearcher().Search("attribute:prod", root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void AttributeValue_Search()
    {
        (Group root, Entry entry) = CreateDbWithEntry();
        entry.Attributes.Set("Server", "prod-01");

        List<Entry> results = CreateSearcher().Search("_Server:prod*", root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void AttributeValue_Search_Exact()
    {
        (Group root, Entry entry) = CreateDbWithEntry();
        entry.Attributes.Set("Env", "staging");

        List<Entry> results = CreateSearcher().Search("_Env:staging", root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void AttributeValue_SkipProtected()
    {
        (Group root, Entry entry) = CreateDbWithEntry();
        entry.Attributes.Set("Token", "abc123", protect: true);

        EntrySearcher searcher = CreateSearcher(skipProtected: true);
        List<Entry> results = searcher.Search("_Token:abc123", root);
        results.Count.ShouldBe(0);
    }

    [Fact]
    public void AttributeValue_Key_Not_Found()
    {
        (Group root, _) = CreateDbWithEntry();

        List<Entry> results = CreateSearcher().Search("_MissingKey:value", root);
        results.Count.ShouldBe(0);
    }

    // ── Attachment search ────────────────────────────────────────────────────

    [Fact]
    public void Attachment_Search()
    {
        (Group root, Entry entry) = CreateDbWithEntry();
        entry.Attachments.Set("id_rsa.pub", [1, 2, 3]);

        List<Entry> results = CreateSearcher().Search("attachment:id_rsa", root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void Attachment_No_Match()
    {
        (Group root, Entry entry) = CreateDbWithEntry();
        entry.Attachments.Set("config.ini", [1, 2, 3]);

        List<Entry> results = CreateSearcher().Search("attachment:id_rsa", root);
        results.Count.ShouldBe(0);
    }

    // ── is: / has: ───────────────────────────────────────────────────────────

    [Fact]
    public void Is_Expired_Already_Expired()
    {
        Group root = new();
        Entry e = new()
        {
            Title = "Old",
            UserName = "bob",
            Times = new Times { Expires = true, ExpiryTime = DateTime.UtcNow.AddDays(-5) },
        };
        root.AddEntry(e);

        List<Entry> results = CreateSearcher().Search("is:expired", root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void Is_Expired_Not_Expired()
    {
        (Group root, Entry entry) = CreateDbWithEntry();
        entry.Times = new Times { Expires = true, ExpiryTime = DateTime.UtcNow.AddDays(30) };

        List<Entry> results = CreateSearcher().Search("is:expired", root);
        results.Count.ShouldBe(0);
    }

    [Fact]
    public void Is_Expired_N_Days()
    {
        (Group root, Entry entry) = CreateDbWithEntry();
        entry.Times = new Times { Expires = true, ExpiryTime = DateTime.UtcNow.AddDays(3) };

        List<Entry> results = CreateSearcher().Search("is:expired-10", root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void Is_Expired_Recycled_Excluded()
    {
        using Database db = Database.Create("pw");
        Entry e = new()
        {
            Title = "Recycled",
            UserName = "bob",
            Times = new Times { Expires = true, ExpiryTime = DateTime.UtcNow.AddDays(-1) },
        };
        db.RootGroup!.AddEntry(e);

        // Move to recycle bin
        e.Delete();

        List<Entry> results = CreateSearcher().Search("is:expired", db.RootGroup);
        results.Count.ShouldBe(0);
    }

    [Fact]
    public void Has_Totp()
    {
        (Group root, Entry entry) = CreateDbWithEntry();
        entry.Attributes.Set("TOTP Seed", "JBSWY3DPEHPK3PXP");

        List<Entry> results = CreateSearcher().Search("has:totp", root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void Has_Totp_Not_Configured()
    {
        (Group root, _) = CreateDbWithEntry();

        List<Entry> results = CreateSearcher().Search("has:totp", root);
        results.Count.ShouldBe(0);
    }

    // ── EnableSearching ──────────────────────────────────────────────────────

    [Fact]
    public void Respects_EnableSearching_Disabled()
    {
        Group root = new() { Name = "Root" };
        Group locked = new() { Name = "Locked", EnableSearching = TriState.Disable };
        root.AddGroup(locked);
        Entry e = new() { Title = "Hidden", UserName = "bob" };
        locked.AddEntry(e);

        List<Entry> results = CreateSearcher().Search("title:Hidden", root);
        results.Count.ShouldBe(0);
    }

    [Fact]
    public void ForceSearch_Overrides_EnableSearching()
    {
        Group root = new() { Name = "Root" };
        Group locked = new() { Name = "Locked", EnableSearching = TriState.Disable };
        root.AddGroup(locked);
        Entry e = new() { Title = "Hidden", UserName = "bob" };
        locked.AddEntry(e);

        List<Entry> results = CreateSearcher().Search("title:Hidden", root, forceSearch: true);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void EnableSearching_Inherit_Defaults_To_Enabled()
    {
        Group root = new() { Name = "Root" };
        Group group = new() { Name = "Normal", EnableSearching = TriState.Inherit };
        root.AddGroup(group);
        Entry e = new() { Title = "Visible", UserName = "bob" };
        group.AddEntry(e);

        List<Entry> results = CreateSearcher().Search("title:Visible", root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void EnableSearching_Inherits_Parent_Disable()
    {
        Group root = new() { Name = "Root", EnableSearching = TriState.Disable };
        Group child = new() { Name = "Child", EnableSearching = TriState.Inherit };
        root.AddGroup(child);
        Entry e = new() { Title = "Hidden", UserName = "bob" };
        child.AddEntry(e);

        List<Entry> results = CreateSearcher().Search("title:Hidden", root);
        results.Count.ShouldBe(0);
    }

    // ── Pre-parsed terms ─────────────────────────────────────────────────────

    [Fact]
    public void Search_With_PreParsed_Terms()
    {
        (Group root, _) = CreateDbWithEntry("GitHub");
        EntrySearcher searcher = CreateSearcher();
        List<SearchTerm> terms = searcher.ParseSearchTerms("title:GitHub");

        List<Entry> results = searcher.Search(terms, root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void SearchEntries_List_Based()
    {
        List<Entry> entries = [];
        for (int i = 0; i < 5; i++)
        {
            entries.Add(new Entry { Title = $"Entry-{i}", UserName = "test" });
        }

        List<Entry> results = CreateSearcher().SearchEntries("title:Entry-3", entries);
        results.Count.ShouldBe(1);
        results[0].Title.ShouldBe("Entry-3");
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public void ParseSearchTerms_Whitespace_Only()
    {
        List<SearchTerm> terms = CreateSearcher().ParseSearchTerms("   ");
        terms.Count.ShouldBe(0);
    }

    [Fact]
    public void ParseSearchTerms_Colon_No_Word()
    {
        List<SearchTerm> terms = CreateSearcher().ParseSearchTerms("title:");
        terms.Count.ShouldBe(0);
    }

    [Fact]
    public void ParseSearchTerms_Field_With_No_Colon_Is_Undefined()
    {
        // "alpha" without colon is a broad search term, not a field
        List<SearchTerm> terms = CreateSearcher().ParseSearchTerms("alpha");
        terms.Count.ShouldBe(1);
        terms[0].Field.ShouldBe(SearchField.Undefined);
    }

    [Fact]
    public void Star_Modifier_Disables_Wildcard_Conversion()
    {
        // With * modifier, "." should be regex dot (match any char)
        Entry e = new() { Title = "aXb", UserName = "bob" };
        Group root = new();
        root.AddEntry(e);

        // Without *: "a.b" would be escaped to "a\.b" then wildcard-converted (nothing since . → \. not wildcard)
        // With *: "a.b" is raw regex where . matches any char
        List<Entry> results = CreateSearcher().Search("*title:a.b", root);
        results.Count.ShouldBe(1);
    }

    [Fact]
    public void Modifiers_Combine()
    {
        (Group root, _) = CreateDbWithEntry("GitHub");
        Entry e2 = new() { Title = "GitLab" };
        root.AddEntry(e2);

        // -!+title:GitHub  → exclude, exact match
        List<Entry> results = CreateSearcher().Search("-+title:GitHub", root);
        results.Count.ShouldBe(1);
        results[0].Title.ShouldBe("GitLab");
    }

    // ── is:expired-0 ────────────────────────────────────────────────────────

    [Fact]
    public void Is_Expired_0_Matches_Already_Expired()
    {
        Entry e = new()
        {
            Title = "Expired",
            UserName = "bob",
            Times = new Times { Expires = true, ExpiryTime = DateTime.UtcNow.AddDays(-1) },
        };
        Group root = new();
        root.AddEntry(e);

        List<Entry> results = CreateSearcher().Search("is:expired-0", root);
        results.Count.ShouldBe(1);
    }

    // ── Undefined field without wildcard ────────────────────────────────────

    [Fact]
    public void Undefined_Search_Without_Wildcard_Matches_Substring()
    {
        (Group root, _) = CreateDbWithEntry("MySecretApp");
        List<Entry> results = CreateSearcher().Search("Secret", root);
        results.Count.ShouldBe(1);
    }
}
