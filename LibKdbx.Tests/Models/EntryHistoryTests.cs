namespace LibKdbx.Tests;

public class EntryHistoryTests
{
    private static Database CreateDb(int maxItems = 10, long maxSize = 6_291_456)
    {
        return new Database
        {
            Metadata = new Metadata { HistoryMaxItems = maxItems, HistoryMaxSize = maxSize },
        };
    }

    [Fact]
    public void Trims_By_Count()
    {
        Database db = CreateDb(maxItems: 2);
        Group root = new();
        Entry e = new();
        root.AddEntry(e);
        root.SetDatabaseRecursive(db);

        // Create 4 updates → history should trim to 2
        for (int i = 0; i < 4; i++)
        {
            e.Update(entry => entry.Title = $"Title{i}");
        }

        e.History.Count.ShouldBe(2);
    }

    [Fact]
    public void Trims_By_Size()
    {
        Database db = CreateDb(maxItems: 100, maxSize: 1000);
        Group root = new();
        Entry e = new();
        root.AddEntry(e);
        root.SetDatabaseRecursive(db);

        // Create updates with large attachments to exceed size limit
        for (int i = 0; i < 5; i++)
        {
            e.Update(entry =>
            {
                entry.Title = $"Title{i}";
                entry.Attachments.Set("big.bin", new byte[400]); // 400 bytes each
            });
        }

        // Total history size: 5 * (5 attr keys * 128 + 400) ≈ 5200 > 1000
        // After trimming, should be ≤ 2 entries
        e.History.Count.ShouldBeLessThanOrEqualTo(2);
    }

    [Fact]
    public void Trims_By_Count_When_Size_Limit_Is_High()
    {
        Database db = CreateDb(maxItems: 3, maxSize: long.MaxValue);
        Group root = new();
        Entry e = new();
        root.AddEntry(e);
        root.SetDatabaseRecursive(db);

        for (int i = 0; i < 6; i++)
        {
            e.Update(entry => entry.Title = $"Title{i}");
        }

        e.History.Count.ShouldBe(3);
    }

    [Fact]
    public void No_Trimming_When_Within_Limits()
    {
        Database db = CreateDb(maxItems: 10, maxSize: long.MaxValue);
        Group root = new();
        Entry e = new();
        root.AddEntry(e);
        root.SetDatabaseRecursive(db);

        for (int i = 0; i < 3; i++)
        {
            e.Update(entry => entry.Title = $"Title{i}");
        }

        e.History.Count.ShouldBe(3);
    }

    [Fact]
    public void History_Snapshots_Are_Independent()
    {
        Database db = CreateDb();
        Group root = new();
        Entry e = new() { Title = "Original" };
        root.AddEntry(e);
        root.SetDatabaseRecursive(db);

        e.Update(entry =>
        {
            entry.Title = "Modified";
            entry.Attributes.Set("Custom", "A");
        });

        Entry snapshot = e.History[0];
        snapshot.Title.ShouldBe("Original");
        snapshot.Attributes.Get("Custom").ShouldBeNull(); // snapshot predates custom attr

        // Mutating snapshot doesn't affect current
        snapshot.Attributes.Set("Custom", "ShouldNotAppear");
        e.Attributes.Get("Custom").ShouldBe("A");
    }
}
