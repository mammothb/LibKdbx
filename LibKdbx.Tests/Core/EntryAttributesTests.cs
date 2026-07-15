namespace LibKdbx.Tests;

public class EntryAttributesTests
{
    // ── Default keys always exist ───────────────────────────────────────────

    [Fact]
    public void New_Has_All_Five_DefaultKeys()
    {
        EntryAttributes attrs = new();
        attrs.Keys.Count.ShouldBe(5);
        foreach (string key in EntryAttributes.DefaultAttributeKeys)
        {
            attrs.Contains(key).ShouldBeTrue($"default key '{key}' should exist");
        }
    }

    [Theory]
    [MemberData(nameof(DefaultKeys))]
    public void DefaultKeys_StartEmpty(string key)
    {
        EntryAttributes attrs = new();
        attrs.Get(key).ShouldBe("");
    }

    [Fact]
    public void Properties_Set_And_Get()
    {
        EntryAttributes attrs = new()
        {
            Title = "MyTitle",
            UserName = "alice",
            Password = "secret",
            Url = "https://example.com",
            Notes = "some notes",
        };

        attrs.Title.ShouldBe("MyTitle");
        attrs.UserName.ShouldBe("alice");
        attrs.Password.ShouldBe("secret");
        attrs.Url.ShouldBe("https://example.com");
        attrs.Notes.ShouldBe("some notes");
        attrs.Get("Title").ShouldBe("MyTitle");
    }

    [Theory]
    [MemberData(nameof(DefaultKeys))]
    public void DefaultKeys_Cannot_Be_Removed(string key)
    {
        EntryAttributes attrs = new();
        attrs.Remove(key).ShouldBeFalse();
        attrs.Contains(key).ShouldBeTrue();
    }

    [Theory]
    [InlineData("tItLe")]
    [InlineData("username")]
    [InlineData("PASSWORD")]
    [InlineData("url")]
    [InlineData("NOTES")]
    public void IsDefaultAttribute_CaseInsensitive(string key)
    {
        EntryAttributes.IsDefaultAttribute(key).ShouldBeTrue();
    }

    // ── Custom keys ─────────────────────────────────────────────────────────

    [Fact]
    public void Set_And_Get_CustomKey()
    {
        EntryAttributes attrs = new();
        attrs.Set("CustomKey", "CustomValue");
        attrs.Get("CustomKey").ShouldBe("CustomValue");
        attrs.Contains("CustomKey").ShouldBeTrue();
    }

    [Fact]
    public void Remove_CustomKey()
    {
        EntryAttributes attrs = new();
        attrs.Set("CustomKey", "value");
        attrs.Remove("CustomKey").ShouldBeTrue();
        attrs.Get("CustomKey").ShouldBeNull();
        attrs.Contains("CustomKey").ShouldBeFalse();
    }

    [Fact]
    public void Rename_CustomKey()
    {
        EntryAttributes attrs = new();
        attrs.Set("OldKey", "value", protect: true);
        attrs.Rename("OldKey", "NewKey").ShouldBeTrue();
        attrs.Get("OldKey").ShouldBeNull();
        attrs.Get("NewKey").ShouldBe("value");
        attrs.IsProtected("NewKey").ShouldBeTrue();
    }

    [Fact]
    public void Rename_PreservesProtection()
    {
        EntryAttributes attrs = new();
        attrs.Set("OldKey", "value", protect: false);
        attrs.Rename("OldKey", "NewKey");
        attrs.IsProtected("NewKey").ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(DefaultKeys))]
    public void Rename_DefaultKey_Fails(string key)
    {
        EntryAttributes attrs = new();
        attrs.Rename(key, "NewKey").ShouldBeFalse();
        attrs.Contains(key).ShouldBeTrue();
    }

    [Fact]
    public void Rename_ToExistingKey_Fails()
    {
        EntryAttributes attrs = new();
        attrs.Set("A", "1");
        attrs.Set("B", "2");
        attrs.Rename("A", "B").ShouldBeFalse();
        attrs.Get("A").ShouldBe("1");
    }

    [Fact]
    public void Rename_NonExisting_OldKey_ReturnsFalse()
    {
        EntryAttributes attrs = new();
        attrs.Rename("DoesNotExist", "NewKey").ShouldBeFalse();
        attrs.Contains("DoesNotExist").ShouldBeFalse();
        attrs.Contains("NewKey").ShouldBeFalse();
    }

    [Theory]
    [InlineData("hello", true)]
    [InlineData("nope", false)]
    public void ContainsValue_Finds_Matches(string search, bool expected)
    {
        EntryAttributes attrs = new() { Title = "hello" };
        attrs.ContainsValue(search).ShouldBe(expected);
    }

    // ── CustomKeys filtering ────────────────────────────────────────────────

    [Fact]
    public void CustomKeys_Excludes_Defaults()
    {
        EntryAttributes attrs = new();
        attrs.Set("MyKey", "val");
        IReadOnlyList<string> custom = attrs.CustomKeys;
        custom.ShouldNotContain("Title");
        custom.ShouldNotContain("UserName");
        custom.ShouldContain("MyKey");
    }

    [Fact]
    public void CustomKeys_Excludes_PasskeyAttributes()
    {
        EntryAttributes attrs = new();
        attrs.Set(EntryAttributes.KPEX_PASSKEY_CREDENTIAL_ID, "cred");
        attrs.Set("MyKey", "val");
        attrs.CustomKeys.ShouldBe(["MyKey"]);
    }

    // ── Protection ──────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(DefaultKeys))]
    public void IsProtected_DefaultsFalse(string key)
    {
        EntryAttributes attrs = new();
        attrs.IsProtected(key).ShouldBeFalse();
    }

    [Fact]
    public void Set_Protect_True()
    {
        EntryAttributes attrs = new();
        attrs.Set("Password", "secret", protect: true);
        attrs.IsProtected("Password").ShouldBeTrue();
    }

    [Fact]
    public void Set_Can_Unprotect()
    {
        EntryAttributes attrs = new();
        attrs.Set("Key", "val", protect: true);
        attrs.IsProtected("Key").ShouldBeTrue();
        attrs.Set("Key", "val", protect: false);
        attrs.IsProtected("Key").ShouldBeFalse();
    }

    [Fact]
    public void Remove_Also_Removes_Protection()
    {
        EntryAttributes attrs = new();
        attrs.Set("Custom", "val", protect: true);
        attrs.Remove("Custom");
        // Protection flag should be gone — re-adding same key starts clean
        attrs.Set("Custom", "new");
        attrs.IsProtected("Custom").ShouldBeFalse();
    }

    // ── References ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("{REF:P@I:ABCDEF1234567890ABCDEF1234567890}", true)]
    [InlineData("{REF:U@T:something}", true)]
    [InlineData("{REF:A@N:notes}", true)]
    [InlineData("plain text", false)]
    [InlineData("", false)]
    [InlineData("{NOT_A_REF}", false)]
    public void IsReference(string value, bool expected)
    {
        EntryAttributes attrs = new();
        attrs.Set("Foo", value);
        attrs.IsReference("Foo").ShouldBe(expected);
    }

    [Fact]
    public void ReferenceUuid_Returns_Uuid_For_SearchIn_I()
    {
        string hex = "ABCDEF1234567890ABCDEF1234567890";
        EntryAttributes attrs = new();
        attrs.Set("Ref", $"{{REF:P@I:{hex}}}");

        Guid guid = attrs.ReferenceUuid("Ref");
        guid.ShouldNotBe(Guid.Empty);
        guid.ToString("N").ToUpperInvariant().ShouldBe(hex);
    }

    [Theory]
    [InlineData("{REF:P@T:sometitle}")]
    [InlineData("{REF:U@N:notes}")]
    [InlineData("not a ref")]
    public void ReferenceUuid_Returns_Empty(string value)
    {
        EntryAttributes attrs = new();
        attrs.Set("Ref", value);
        attrs.ReferenceUuid("Ref").ShouldBe(Guid.Empty);
    }

    // ── Passkey ─────────────────────────────────────────────────────────────

    [Fact]
    public void HasPasskey_True_When_PasskeyAttribute_Present()
    {
        EntryAttributes attrs = new();
        attrs.Set(EntryAttributes.KPEX_PASSKEY_CREDENTIAL_ID, "cred");
        attrs.HasPasskey().ShouldBeTrue();
    }

    [Fact]
    public void HasPasskey_False_By_Default()
    {
        new EntryAttributes().HasPasskey().ShouldBeFalse();
    }

    [Fact]
    public void RemovePasskeyAttributes_Removes_All_PasskeyKeys()
    {
        EntryAttributes attrs = new();
        attrs.Set(EntryAttributes.KPEX_PASSKEY_CREDENTIAL_ID, "cred");
        attrs.Set(EntryAttributes.KPEX_PASSKEY_USERNAME, "alice");
        attrs.Set("MyKey", "val");

        attrs.RemovePasskeyAttributes();

        attrs.HasPasskey().ShouldBeFalse();
        attrs.Contains("MyKey").ShouldBeTrue();
    }

    // ── URLs ────────────────────────────────────────────────────────────────

    [Fact]
    public void GetAllUrls_Returns_Primary_And_Additional()
    {
        EntryAttributes attrs = new() { Url = "https://primary.com" };
        attrs.Set("KP2A_URL_1", "https://secondary.com");
        attrs.Set("KP2A_URL_2", "https://tertiary.com");

        IReadOnlyList<string> urls = attrs.GetAllUrls();
        urls.Count.ShouldBe(3);
        urls[0].ShouldBe("https://primary.com");
        urls[1].ShouldBe("https://secondary.com");
        urls[2].ShouldBe("https://tertiary.com");
    }

    [Fact]
    public void GetAdditionalUrls_Excludes_Primary()
    {
        EntryAttributes attrs = new() { Url = "https://primary.com" };
        attrs.Set("KP2A_URL_1", "https://secondary.com");

        IReadOnlyList<string> additional = attrs.GetAdditionalUrls();
        additional.Count.ShouldBe(1);
        additional[0].ShouldBe("https://secondary.com");
    }

    [Theory]
    [InlineData("https://primary.com", "https://secondary.com", "https://primary.com")]
    [InlineData("", "https://secondary.com", "https://secondary.com")]
    [InlineData("", "", null)]
    public void ResolveUrl(string primary, string additional, string? expected)
    {
        EntryAttributes attrs = new();
        if (!string.IsNullOrEmpty(primary))
        {
            attrs.Url = primary;
        }
        if (!string.IsNullOrEmpty(additional))
        {
            attrs.Set("KP2A_URL_1", additional);
        }
        attrs.ResolveUrl().ShouldBe(expected);
    }

    // ── Bulk operations ─────────────────────────────────────────────────────

    [Fact]
    public void Clear_Keeps_Defaults_Empty_Removes_Custom()
    {
        EntryAttributes attrs = new() { Title = "title" };
        attrs.Set("Custom", "val");
        attrs.Clear();

        attrs.Title.ShouldBe("");
        attrs.Contains("Custom").ShouldBeFalse();
        attrs.Keys.Count.ShouldBe(5);
    }

    [Fact]
    public void CopyCustomKeysFrom_Replaces_CustomKeys()
    {
        EntryAttributes src = new();
        src.Set("A", "1");
        src.Set("B", "2", protect: true);

        EntryAttributes dst = new();
        dst.Set("Old", "old");
        dst.Title = "dstTitle";

        dst.CopyCustomKeysFrom(src);

        dst.Title.ShouldBe("dstTitle"); // default untouched
        dst.Contains("Old").ShouldBeFalse();
        dst.Get("A").ShouldBe("1");
        dst.Get("B").ShouldBe("2");
        dst.IsProtected("B").ShouldBeTrue();
    }

    [Fact]
    public void AreCustomKeysDifferent_SameKeys_ReturnsFalse()
    {
        EntryAttributes a = new();
        a.Set("X", "1");
        EntryAttributes b = new();
        b.Set("X", "1");

        a.AreCustomKeysDifferent(b).ShouldBeFalse();
    }

    [Fact]
    public void AreCustomKeysDifferent_DifferentKeys_ReturnsTrue()
    {
        EntryAttributes a = new();
        a.Set("X", "1");
        EntryAttributes b = new();
        b.Set("Y", "1");

        a.AreCustomKeysDifferent(b).ShouldBeTrue();
    }

    [Fact]
    public void AreCustomKeysDifferent_DifferentValues_ReturnsTrue()
    {
        EntryAttributes a = new();
        a.Set("X", "val-a");
        EntryAttributes b = new();
        b.Set("X", "val-b");

        a.AreCustomKeysDifferent(b).ShouldBeTrue();
    }

    [Fact]
    public void AreCustomKeysDifferent_SameValuesDifferentProtection_ReturnsTrue()
    {
        EntryAttributes a = new();
        a.Set("X", "val", protect: true);
        EntryAttributes b = new();
        b.Set("X", "val", protect: false);

        a.AreCustomKeysDifferent(b).ShouldBeTrue();
    }

    [Fact]
    public void CopyFrom_Copies_Everything()
    {
        EntryAttributes src = new() { Title = "T" };
        src.Set("X", "1", protect: true);

        EntryAttributes dst = new();
        dst.CopyFrom(src);

        dst.Title.ShouldBe("T");
        dst.Get("X").ShouldBe("1");
        dst.IsProtected("X").ShouldBeTrue();
    }

    // ── Clone ───────────────────────────────────────────────────────────────

    [Fact]
    public void Clone_Is_Independent()
    {
        EntryAttributes orig = new() { Title = "Original" };
        orig.Set("Custom", "value", protect: true);

        EntryAttributes clone = orig.Clone();
        clone.Title = "Cloned";

        orig.Title.ShouldBe("Original");
        clone.Title.ShouldBe("Cloned");
    }

    // ── Equality ────────────────────────────────────────────────────────────

    [Fact]
    public void Equals_SameContent_ReturnsTrue()
    {
        EntryAttributes a = new() { Title = "T" };
        a.Set("X", "1", protect: true);

        EntryAttributes b = new() { Title = "T" };
        b.Set("X", "1", protect: true);

        a.Equals(b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentProtection_ReturnsFalse()
    {
        EntryAttributes a = new();
        a.Set("X", "1", protect: true);

        EntryAttributes b = new();
        b.Set("X", "1", protect: false);

        a.Equals(b).ShouldBeFalse();
    }

    [Fact]
    public void Keys_Preserves_InsertionOrder()
    {
        EntryAttributes attrs = new() { Title = "T1" };
        attrs.Set("Z", "z");
        attrs.Set("A", "a");

        IReadOnlyList<string> keys = attrs.Keys;
        // Default keys come first in their defined order, then custom in insertion order
        keys[0].ShouldBe("Title");
        keys[4].ShouldBe("Notes");
        keys[5].ShouldBe("Z");
        keys[6].ShouldBe("A");
    }

    // ── Protection tracks changes on writes ─────────────────────────────────

    [Fact]
    public void Set_Without_Protect_Removes_Protection()
    {
        EntryAttributes attrs = new();
        attrs.Set("Key", "val1", protect: true);
        attrs.IsProtected("Key").ShouldBeTrue();

        attrs.Set("Key", "val2"); // no protect param = false → removes protection
        attrs.IsProtected("Key").ShouldBeFalse();
    }

    // ── Member data ─────────────────────────────────────────────────────────

    public static TheoryData<string> DefaultKeys => [.. EntryAttributes.DefaultAttributeKeys];
}
