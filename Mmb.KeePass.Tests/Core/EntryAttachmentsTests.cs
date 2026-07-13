namespace LibKdbx.Tests;

public class EntryAttachmentsTests
{
    [Fact]
    public void New_IsEmpty()
    {
        EntryAttachments attachments = new();
        attachments.IsEmpty.ShouldBeTrue();
        attachments.Count.ShouldBe(0);
    }

    [Fact]
    public void Set_And_Get()
    {
        EntryAttachments attachments = new();
        byte[] data = [1, 2, 3, 4];

        attachments.Set("file.bin", data);

        attachments.Get("file.bin").ShouldBe(data);
        attachments.Contains("file.bin").ShouldBeTrue();
        attachments.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Set_Overwrites_Existing()
    {
        EntryAttachments attachments = new();
        attachments.Set("key", [1, 2]);
        attachments.Set("key", [3, 4, 5]);

        attachments.Get("key").ShouldBe([3, 4, 5]);
        attachments.Count.ShouldBe(1);
    }

    [Fact]
    public void Get_Missing_Returns_Null()
    {
        EntryAttachments attachments = new();
        attachments.Get("nope").ShouldBeNull();
    }

    [Fact]
    public void Remove_Existing()
    {
        EntryAttachments attachments = new();
        attachments.Set("key", [1, 2]);
        attachments.Remove("key").ShouldBeTrue();
        attachments.Get("key").ShouldBeNull();
        attachments.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Remove_Missing_Returns_False()
    {
        new EntryAttachments().Remove("nope").ShouldBeFalse();
    }

    [Fact]
    public void Rename_Success()
    {
        EntryAttachments attachments = new();
        byte[] data = [1, 2, 3];
        attachments.Set("old.txt", data);

        attachments.Rename("old.txt", "new.txt").ShouldBeTrue();

        attachments.Get("old.txt").ShouldBeNull();
        attachments.Get("new.txt").ShouldBe(data);
    }

    [Fact]
    public void Rename_MissingKey_Fails()
    {
        EntryAttachments attachments = new();
        attachments.Rename("nope", "dest").ShouldBeFalse();
    }

    [Fact]
    public void Rename_ToExisting_Fails()
    {
        EntryAttachments attachments = new();
        attachments.Set("a", [1]);
        attachments.Set("b", [2]);

        attachments.Rename("a", "b").ShouldBeFalse();
        attachments.Get("a").ShouldBe([1]); // unchanged
        attachments.Get("b").ShouldBe([2]); // unchanged
    }

    [Fact]
    public void Clear_Removes_All()
    {
        EntryAttachments attachments = new();
        attachments.Set("a", [1]);
        attachments.Set("b", [2]);

        attachments.Clear();

        attachments.IsEmpty.ShouldBeTrue();
        attachments.Keys.Count.ShouldBe(0);
    }

    [Fact]
    public void DataSize_Returns_TotalBytes()
    {
        EntryAttachments attachments = new();
        attachments.Set("a", [1, 2, 3]);
        attachments.Set("b", [4, 5]);

        attachments.DataSize().ShouldBe(5);
    }

    [Fact]
    public void DataSize_Empty_Is_Zero()
    {
        new EntryAttachments().DataSize().ShouldBe(0);
    }

    [Fact]
    public void Keys_Returns_All_Names()
    {
        EntryAttachments attachments = new();
        attachments.Set("z.txt", [1]);
        attachments.Set("a.txt", [2]);
        attachments.Set("m.txt", [3]);

        attachments.Keys.ShouldBe(["z.txt", "a.txt", "m.txt"]);
    }

    [Fact]
    public void CopyFrom_Copies_All()
    {
        EntryAttachments src = new();
        byte[] data = [1, 2, 3];
        src.Set("file.bin", data);

        EntryAttachments dst = new();
        dst.CopyFrom(src);

        dst.Get("file.bin").ShouldBe(data);
        dst.Count.ShouldBe(1);
    }

    [Fact]
    public void CopyFrom_Deep_Copies_Data()
    {
        EntryAttachments src = new();
        byte[] data = [1, 2, 3];
        src.Set("file.bin", data);

        EntryAttachments dst = new();
        dst.CopyFrom(src);

        // Mutate the source data — destination must be unaffected
        data[0] = 99;
        src.Set("file.bin", data);

        dst.Get("file.bin").ShouldBe([1, 2, 3]); // original copy preserved
    }

    [Fact]
    public void CopyFrom_Overwrites_Existing()
    {
        EntryAttachments src = new();
        src.Set("key", [9, 9]);

        EntryAttachments dst = new();
        dst.Set("key", [1, 1]);
        dst.Set("other", [2, 2]);

        dst.CopyFrom(src);

        dst.Get("key").ShouldBe([9, 9]);
        dst.Contains("other").ShouldBeFalse();
        dst.Count.ShouldBe(1);
    }

    [Fact]
    public void Clone_Is_Independent()
    {
        EntryAttachments orig = new();
        orig.Set("file.bin", [1, 2, 3]);

        EntryAttachments clone = orig.Clone();
        clone.Set("file.bin", [4, 5, 6]);

        orig.Get("file.bin").ShouldBe([1, 2, 3]);
        clone.Get("file.bin").ShouldBe([4, 5, 6]);
    }

    [Fact]
    public void Equals_SameContent_ReturnsTrue()
    {
        EntryAttachments a = new();
        a.Set("file.bin", [1, 2, 3]);

        EntryAttachments b = new();
        b.Set("file.bin", [1, 2, 3]);

        a.Equals(b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentData_ReturnsFalse()
    {
        EntryAttachments a = new();
        a.Set("file.bin", [1, 2, 3]);

        EntryAttachments b = new();
        b.Set("file.bin", [4, 5, 6]);

        a.Equals(b).ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentKeys_ReturnsFalse()
    {
        EntryAttachments a = new();
        a.Set("a.txt", [1]);

        EntryAttachments b = new();
        b.Set("b.txt", [1]);

        a.Equals(b).ShouldBeFalse();
    }
}
