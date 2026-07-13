namespace LibKdbx.Tests;

public class BinaryPoolBuilderTests
{
    [Fact]
    public void Empty_Group_Produces_Empty_Pool()
    {
        Group root = new();
        BinaryPool pool = BinaryPoolBuilder.Build(root);

        pool.Count.ShouldBe(0);
    }

    [Fact]
    public void Deduplicates_Identical_Data()
    {
        byte[] data = [1, 2, 3, 4, 5];

        Entry e1 = new();
        e1.Attachments.Set("a.bin", data);
        Entry e2 = new();
        e2.Attachments.Set("b.bin", data); // same content, different filename
        Entry e3 = new();
        e3.Attachments.Set("c.bin", data); // third copy

        Group root = new();
        root.AddEntry(e1);
        root.AddEntry(e2);
        root.AddEntry(e3);

        BinaryPool pool = BinaryPoolBuilder.Build(root);

        pool.Count.ShouldBe(1);

        int idx = pool.GetIndex(data);
        idx.ShouldBe(0);
    }

    [Fact]
    public void Different_Data_Kept_Separate()
    {
        byte[] data1 = [1, 2, 3];
        byte[] data2 = [4, 5, 6];

        Entry e1 = new();
        e1.Attachments.Set("a.bin", data1);
        Entry e2 = new();
        e2.Attachments.Set("b.bin", data2);

        Group root = new();
        root.AddEntry(e1);
        root.AddEntry(e2);

        BinaryPool pool = BinaryPoolBuilder.Build(root);

        pool.Count.ShouldBe(2);
        pool.GetIndex(data1).ShouldBe(0);
        pool.GetIndex(data2).ShouldBe(1);
    }

    [Fact]
    public void Includes_History_Attachments()
    {
        byte[] data = [1, 2, 3];

        Entry e = new();
        e.Attachments.Set("current.bin", [9, 9, 9]);
        // Create a history entry with different attachment
        Entry snapshot = new();
        snapshot.Attachments.Set("old.bin", data);
        e.History.Add(snapshot);

        Group root = new();
        root.AddEntry(e);

        BinaryPool pool = BinaryPoolBuilder.Build(root);

        pool.Count.ShouldBe(2); // current + history attachment
        pool.GetIndex(data).ShouldNotBe(-1);
    }

    [Fact]
    public void Includes_Nested_Groups()
    {
        byte[] data = [1, 2, 3];

        Entry e = new();
        e.Attachments.Set("file.bin", data);

        Group child = new();
        child.AddEntry(e);

        Group root = new();
        root.AddGroup(child);

        BinaryPool pool = BinaryPoolBuilder.Build(root);

        pool.Count.ShouldBe(1);
    }

    [Fact]
    public void GetIndex_Missing_Returns_Negative_One()
    {
        Entry e = new();
        e.Attachments.Set("file.bin", [1, 2, 3]);

        Group root = new();
        root.AddEntry(e);

        BinaryPool pool = BinaryPoolBuilder.Build(root);

        pool.GetIndex([9, 9, 9]).ShouldBe(-1);
    }

    [Fact]
    public void GetIndex_Empty_Data_Returns_Negative_One()
    {
        Entry e = new();
        e.Attachments.Set("file.bin", [1, 2, 3]);

        Group root = new();
        root.AddEntry(e);

        BinaryPool pool = BinaryPoolBuilder.Build(root);

        pool.GetIndex([]).ShouldBe(-1);
    }
}
