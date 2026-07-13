namespace Mmb.KeePass.Tests;

public class EntryAttributesTests
{
    // ── Default keys always exist ───────────────────────────────────────────

    [Fact]
    public void New_Has_All_Five_DefaultKeys()
    {
        var attrs = new EntryAttributes();
        attrs.Keys.Count.ShouldBe(5);
        foreach (string key in EntryAttributes.DefaultAttributeKeys)
        {
            attrs.Contains(key).ShouldBeTrue($"default key '{key}' should exist");
        }
    }

    [Fact]
    public void DefaultKeys_StartEmpty()
    {
        var attrs = new EntryAttributes();
        attrs.Title.ShouldBe("");
        attrs.UserName.ShouldBe("");
        attrs.Password.ShouldBe("");
        attrs.Url.ShouldBe("");
        attrs.Notes.ShouldBe("");
    }

    [Fact]
    public void Properties_Set_And_Get()
    {
        var attrs = new EntryAttributes
        {
            Title = "MyTitle",
            UserName = "alice",
            Password = "secret",
            Url = "https://example.com",
            Notes = "some notes"
        };

        attrs.Title.ShouldBe("MyTitle");
        attrs.UserName.ShouldBe("alice");
        attrs.Password.ShouldBe("secret");
        attrs.Url.ShouldBe("https://example.com");
        attrs.Notes.ShouldBe("some notes");
        attrs.Get("Title").ShouldBe("MyTitle");
    }

    [Fact]
    public void DefaultKeys_Cannot_Be_Removed()
    {
        var attrs = new EntryAttributes();
        attrs.Remove("Title").ShouldBeFalse();
        attrs.Contains("Title").ShouldBeTrue();
    }

    [Fact]
    public void DefaultKeys_Ignore_IsDefaultAttribute_CaseInsensitive()
    {
        EntryAttributes.IsDefaultAttribute("tItLe").ShouldBeTrue();
        EntryAttributes.IsDefaultAttribute("username").ShouldBeTrue();
    }

    // ── Custom keys ─────────────────────────────────────────────────────────

    [Fact]
    public void Set_And_Get_CustomKey()
    {
        var attrs = new EntryAttributes();
        attrs.Set("CustomKey", "CustomValue");
        attrs.Get("CustomKey").ShouldBe("CustomValue");
        attrs.Contains("CustomKey").ShouldBeTrue();
    }

    [Fact]
    public void Remove_CustomKey()
    {
        var attrs = new EntryAttributes();
        attrs.Set("CustomKey", "value");
        attrs.Remove("CustomKey").ShouldBeTrue();
        attrs.Get("CustomKey").ShouldBeNull();
        attrs.Contains("CustomKey").ShouldBeFalse();
    }

    [Fact]
    public void Rename_CustomKey()
    {
        var attrs = new EntryAttributes();
        attrs.Set("OldKey", "value", protect: true);
        attrs.Rename("OldKey", "NewKey").ShouldBeTrue();
        attrs.Get("OldKey").ShouldBeNull();
        attrs.Get("NewKey").ShouldBe("value");
        attrs.IsProtected("NewKey").ShouldBeTrue();
    }

    [Fact]
    public void Rename_PreservesProtection()
    {
        var attrs = new EntryAttributes();
        attrs.Set("OldKey", "value", protect: false);
        attrs.Rename("OldKey", "NewKey");
        attrs.IsProtected("NewKey").ShouldBeFalse();
    }

    [Fact]
    public void Rename_DefaultKey_Fails()
    {
        var attrs = new EntryAttributes();
        attrs.Rename("Title", "NewTitle").ShouldBeFalse();
        attrs.Contains("Title").ShouldBeTrue();
    }

    [Fact]
    public void Rename_ToExistingKey_Fails()
    {
        var attrs = new EntryAttributes();
        attrs.Set("A", "1");
        attrs.Set("B", "2");
        attrs.Rename("A", "B").ShouldBeFalse();
        attrs.Get("A").ShouldBe("1");
    }

    [Fact]
    public void ContainsValue_Finds_Matches()
    {
        var attrs = new EntryAttributes
        {
            Title = "hello"
        };
        attrs.ContainsValue("hello").ShouldBeTrue();
        attrs.ContainsValue("nope").ShouldBeFalse();
    }

    // ── CustomKeys filtering ────────────────────────────────────────────────

    [Fact]
    public void CustomKeys_Excludes_Defaults()
    {
        var attrs = new EntryAttributes();
        attrs.Set("MyKey", "val");
        IReadOnlyList<string> custom = attrs.CustomKeys;
        custom.ShouldNotContain("Title");
        custom.ShouldNotContain("UserName");
        custom.ShouldContain("MyKey");
    }

    [Fact]
    public void CustomKeys_Excludes_PasskeyAttributes()
    {
        var attrs = new EntryAttributes();
        attrs.Set(EntryAttributes.KPEX_PASSKEY_CREDENTIAL_ID, "cred");
        attrs.Set("MyKey", "val");
        attrs.CustomKeys.ShouldBe(["MyKey"]);
    }

    // ── Protection ──────────────────────────────────────────────────────────

    [Fact]
    public void IsProtected_DefaultsFalse()
    {
        var attrs = new EntryAttributes();
        attrs.IsProtected("Title").ShouldBeFalse();
        attrs.IsProtected("Password").ShouldBeFalse();
    }

    [Fact]
    public void Set_Protect_True()
    {
        var attrs = new EntryAttributes();
        attrs.Set("Password", "secret", protect: true);
        attrs.IsProtected("Password").ShouldBeTrue();
    }

    [Fact]
    public void Set_Can_Unprotect()
    {
        var attrs = new EntryAttributes();
        attrs.Set("Key", "val", protect: true);
        attrs.IsProtected("Key").ShouldBeTrue();
        attrs.Set("Key", "val", protect: false);
        attrs.IsProtected("Key").ShouldBeFalse();
    }

    [Fact]
    public void Remove_Also_Removes_Protection()
    {
        var attrs = new EntryAttributes();
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
        var attrs = new EntryAttributes();
        attrs.Set("Foo", value);
        attrs.IsReference("Foo").ShouldBe(expected);
    }

    [Fact]
    public void ReferenceUuid_Returns_Uuid_For_SearchIn_I()
    {
        string hex = "ABCDEF1234567890ABCDEF1234567890";
        var attrs = new EntryAttributes();
        attrs.Set("Ref", $"{{REF:P@I:{hex}}}");

        Guid guid = attrs.ReferenceUuid("Ref");
        guid.ShouldNotBe(Guid.Empty);
        guid.ToString("N").ToUpperInvariant().ShouldBe(hex);
    }

    [Fact]
    public void ReferenceUuid_Returns_Empty_For_SearchIn_T()
    {
        var attrs = new EntryAttributes();
        attrs.Set("Ref", "{REF:P@T:sometitle}");
        attrs.ReferenceUuid("Ref").ShouldBe(Guid.Empty);
    }

    [Fact]
    public void ReferenceUuid_Returns_Empty_For_NonReference()
    {
        var attrs = new EntryAttributes();
        attrs.Set("Ref", "not a ref");
        attrs.ReferenceUuid("Ref").ShouldBe(Guid.Empty);
    }

    // ── Passkey ─────────────────────────────────────────────────────────────

    [Fact]
    public void HasPasskey_True_When_PasskeyAttribute_Present()
    {
        var attrs = new EntryAttributes();
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
        var attrs = new EntryAttributes();
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
        var attrs = new EntryAttributes
        {
            Url = "https://primary.com"
        };
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
        var attrs = new EntryAttributes
        {
            Url = "https://primary.com"
        };
        attrs.Set("KP2A_URL_1", "https://secondary.com");

        IReadOnlyList<string> additional = attrs.GetAdditionalUrls();
        additional.Count.ShouldBe(1);
        additional[0].ShouldBe("https://secondary.com");
    }

    [Fact]
    public void ResolveUrl_Primary_Takes_Precedence()
    {
        var attrs = new EntryAttributes
        {
            Url = "https://primary.com"
        };
        attrs.Set("KP2A_URL_1", "https://secondary.com");

        attrs.ResolveUrl().ShouldBe("https://primary.com");
    }

    [Fact]
    public void ResolveUrl_Falls_Back_To_FirstAdditional()
    {
        var attrs = new EntryAttributes();
        attrs.Set("KP2A_URL_1", "https://secondary.com");

        attrs.ResolveUrl().ShouldBe("https://secondary.com");
    }

    [Fact]
    public void ResolveUrl_Returns_Null_When_None()
    {
        new EntryAttributes().ResolveUrl().ShouldBeNull();
    }

    // ── Bulk operations ─────────────────────────────────────────────────────

    [Fact]
    public void Clear_Keeps_Defaults_Empty_Removes_Custom()
    {
        var attrs = new EntryAttributes
        {
            Title = "title"
        };
        attrs.Set("Custom", "val");
        attrs.Clear();

        attrs.Title.ShouldBe("");
        attrs.Contains("Custom").ShouldBeFalse();
        attrs.Keys.Count.ShouldBe(5);
    }

    [Fact]
    public void CopyCustomKeysFrom_Replaces_CustomKeys()
    {
        var src = new EntryAttributes();
        src.Set("A", "1");
        src.Set("B", "2", protect: true);

        var dst = new EntryAttributes();
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
        var a = new EntryAttributes();
        a.Set("X", "1");
        var b = new EntryAttributes();
        b.Set("X", "1");

        a.AreCustomKeysDifferent(b).ShouldBeFalse();
    }

    [Fact]
    public void AreCustomKeysDifferent_DifferentKeys_ReturnsTrue()
    {
        var a = new EntryAttributes();
        a.Set("X", "1");
        var b = new EntryAttributes();
        b.Set("Y", "1");

        a.AreCustomKeysDifferent(b).ShouldBeTrue();
    }

    [Fact]
    public void CopyFrom_Copies_Everything()
    {
        var src = new EntryAttributes
        {
            Title = "T"
        };
        src.Set("X", "1", protect: true);

        var dst = new EntryAttributes();
        dst.CopyFrom(src);

        dst.Title.ShouldBe("T");
        dst.Get("X").ShouldBe("1");
        dst.IsProtected("X").ShouldBeTrue();
    }

    // ── Clone ───────────────────────────────────────────────────────────────

    [Fact]
    public void Clone_Is_Independent()
    {
        var orig = new EntryAttributes
        {
            Title = "Original"
        };
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
        var a = new EntryAttributes
        {
            Title = "T"
        };
        a.Set("X", "1", protect: true);

        var b = new EntryAttributes
        {
            Title = "T"
        };
        b.Set("X", "1", protect: true);

        a.Equals(b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentProtection_ReturnsFalse()
    {
        var a = new EntryAttributes();
        a.Set("X", "1", protect: true);

        var b = new EntryAttributes();
        b.Set("X", "1", protect: false);

        a.Equals(b).ShouldBeFalse();
    }

    [Fact]
    public void Keys_Preserves_InsertionOrder()
    {
        var attrs = new EntryAttributes
        {
            Title = "T1"   // first in DefaultAttributeKeys order
        };
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
    public void Set_Without_Protect_Leaves_ProtectionUnchanged()
    {
        var attrs = new EntryAttributes();
        attrs.Set("Key", "val1", protect: true);
        attrs.IsProtected("Key").ShouldBeTrue();

        attrs.Set("Key", "val2"); // no protect param = false → removes protection
        attrs.IsProtected("Key").ShouldBeFalse();
    }
}
