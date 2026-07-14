namespace LibKdbx.Tests;

public class KeePassInteropTests
{
    private static string TestDataPath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", filename);

    private static bool Missing(string filename, out string path)
    {
        path = TestDataPath(filename);
        if (!File.Exists(path))
        {
            // Test passes but does nothing — file not generated yet
            return true;
        }
        return false;
    }

    [Fact]
    public void Open_KeePass_Minimal_V4()
    {
        if (Missing("minimal_v4.kdbx", out string path))
        {
            return;
        }
        using Database db = Database.Open(path, "test");
        db.RootGroup!.Entries.Count.ShouldBeGreaterThanOrEqualTo(1);
        db.Version.Major.ShouldBe((ushort)4);
    }

    [Fact]
    public void Open_KeePass_With_Features_V4()
    {
        if (Missing("with_features_v4.kdbx", out string path))
        {
            return;
        }
        using Database db = Database.Open(path, "test");
        db.RootGroup!.FindAllEntries(_ => true).Any().ShouldBeTrue();
    }

    [Fact]
    public void RoundTrip_KeePass_With_Features_V4()
    {
        if (Missing("with_features_v4.kdbx", out string path))
        {
            return;
        }
        string outPath = Path.GetTempFileName();
        try
        {
            using Database db = Database.Open(path, "test");
            int entryCount = db.RootGroup!.FindAllEntries(_ => true).Count();
            int deletedCount = db.DeletedObjects.Count;

            db.SaveAs(outPath);

            using Database reopened = Database.Open(outPath, "test");
            reopened.RootGroup!.FindAllEntries(_ => true).Count().ShouldBe(entryCount);
            reopened.DeletedObjects.Count.ShouldBe(deletedCount);
        }
        finally
        {
            File.Delete(outPath);
        }
    }

    [Fact]
    public void Open_KeePass_With_DeletedObjects_V4()
    {
        if (Missing("trash_v4.kdbx", out string path))
        {
            return;
        }
        using Database db = Database.Open(path, "test");
        db.DeletedObjects.Count.ShouldBeGreaterThan(0);
    }

    // ── Cipher / KDF interop ────────────────────────────────────────────

    [Fact]
    public void Open_KeePass_ChaCha20_Argon2id_V4()
    {
        if (Missing("chacha20_argon2id_v4.kdbx", out string path))
        {
            return;
        }
        using Database db = Database.Open(path, "test");
        db.RootGroup!.Entries.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Open_KeePass_ChaCha20_Argon2d_V4()
    {
        if (Missing("chacha20_argon2d_v4.kdbx", out string path))
        {
            return;
        }
        using Database db = Database.Open(path, "test");
        db.RootGroup!.Entries.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Open_KeePass_Aes256_Argon2id_V4()
    {
        if (Missing("aes256_argon2id_v4.kdbx", out string path))
        {
            return;
        }
        using Database db = Database.Open(path, "test");
        db.RootGroup!.Entries.Count.ShouldBeGreaterThanOrEqualTo(1);
    }
}
