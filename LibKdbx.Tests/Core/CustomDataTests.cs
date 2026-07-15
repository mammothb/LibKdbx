namespace LibKdbx.Tests;

public class CustomDataItemTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var item = new CustomDataItem("hello", DateTime.UnixEpoch);
        item.Value.ShouldBe("hello");
        item.LastModified.ShouldBe(DateTime.UnixEpoch);
    }

    [Fact]
    public void Default_LastModified_Is_Null()
    {
        var item = new CustomDataItem("hello");
        item.LastModified.ShouldBeNull();
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = new CustomDataItem("foo", DateTime.UnixEpoch);
        var b = new CustomDataItem("foo", DateTime.UnixEpoch);
        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        var a = new CustomDataItem("foo");
        var b = new CustomDataItem("bar");
        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_DifferentLastModified_AreNotEqual()
    {
        var a = new CustomDataItem("foo", DateTime.UnixEpoch);
        var b = new CustomDataItem("foo", DateTime.UnixEpoch.AddDays(1));
        a.ShouldNotBe(b);
    }
}

public class CustomDataTests
{
    [Fact]
    public void New_IsEmpty()
    {
        var cd = new CustomData();
        cd.IsEmpty.ShouldBeTrue();
        cd.Count.ShouldBe(0);
    }

    [Fact]
    public void Set_And_Get()
    {
        var cd = new CustomData();
        cd.Set("key", "value");
        cd.GetValue("key").ShouldBe("value");
        cd.ContainsKey("key").ShouldBeTrue();
        cd.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Set_With_Timestamp()
    {
        DateTime ts = DateTime.UtcNow;
        var cd = new CustomData();
        cd.Set("key", "value", ts);
        cd.GetItem("key")!.Value.LastModified.ShouldBe(ts);
    }

    [Fact]
    public void Set_Overwrites_Existing()
    {
        var cd = new CustomData();
        cd.Set("key", "first");
        cd.Set("key", "second");
        cd.GetValue("key").ShouldBe("second");
        cd.Count.ShouldBe(1);
    }

    [Fact]
    public void Remove_Existing_Key()
    {
        var cd = new CustomData();
        cd.Set("key", "value");
        cd.Remove("key").ShouldBeTrue();
        cd.GetValue("key").ShouldBeNull();
        cd.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Remove_Missing_Key_Returns_False()
    {
        var cd = new CustomData();
        cd.Remove("nope").ShouldBeFalse();
    }

    [Fact]
    public void Clear_Empties_All()
    {
        var cd = new CustomData();
        cd.Set("a", "1");
        cd.Set("b", "2");
        cd.Clear();
        cd.IsEmpty.ShouldBeTrue();
        cd.Keys.Count.ShouldBe(0);
    }

    [Fact]
    public void Indexer_GetSet()
    {
        var cd = new CustomData();
        cd["key"] = "value";
        cd["key"].ShouldBe("value");
    }

    [Fact]
    public void Indexer_Null_Removes()
    {
        var cd = new CustomData();
        cd["key"] = "value";
        cd["key"] = null;
        cd.ContainsKey("key").ShouldBeFalse();
    }

    [Fact]
    public void Keys_Preserves_Insertion_Order()
    {
        var cd = new CustomData();
        cd.Set("z", "1");
        cd.Set("a", "2");
        cd.Set("m", "3");
        cd.Keys.ShouldBe(["z", "a", "m"]);
    }

    [Fact]
    public void CopyFrom_Copies_All_Items()
    {
        var src = new CustomData();
        DateTime ts = DateTime.UtcNow;
        src.Set("a", "1", ts);
        src.Set("b", "2");

        var dst = new CustomData();
        dst.CopyFrom(src);

        dst.Count.ShouldBe(2);
        dst.GetValue("a").ShouldBe("1");
        dst.GetItem("a")!.Value.LastModified.ShouldBe(ts);
        dst.GetValue("b").ShouldBe("2");
        dst.GetItem("b")!.Value.LastModified.ShouldBeNull();
    }

    [Fact]
    public void CopyFrom_Overwrites_Existing()
    {
        var src = new CustomData();
        src.Set("key", "new");

        var dst = new CustomData();
        dst.Set("key", "old");
        dst.Set("other", "keep");

        dst.CopyFrom(src);

        dst.GetValue("key").ShouldBe("new");
        dst.ContainsKey("other").ShouldBeFalse();
        dst.Count.ShouldBe(1);
    }

    [Fact]
    public void TryGetValue_MissingKey_ReturnsFalse()
    {
        var cd = new CustomData();
        cd.TryGetValue("nope", out string? value).ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public void GetItem_Missing_Key_Returns_Null()
    {
        var cd = new CustomData();
        cd.GetItem("nope").ShouldBeNull();
    }
}
