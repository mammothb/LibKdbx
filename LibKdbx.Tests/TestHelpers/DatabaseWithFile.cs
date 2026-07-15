namespace LibKdbx.Tests;

/// <summary>
/// Disposable database + temp file for save/load round-trip tests.
/// Creates a <see cref="Database"/> with an auto-generated temp file path.
/// <see cref="Save"/> writes the database to the temp file, and
/// <see cref="Open"/> reads it back.
/// Both database and temp file are cleaned up on <see cref="Dispose"/>.
/// </summary>
public sealed class DatabaseWithFile : IDisposable
{
    private readonly TempFile _file = new();
    public Database Database { get; }
    public string Path => _file.Path;

    public DatabaseWithFile(string password = "pw", Settings? settings = null)
    {
        Database = Database.Create(password, settings);
    }

    public void Save() => Database.SaveAs(Path);

    public async Task SaveAsync(CancellationToken ct = default) =>
        await Database.SaveAsAsync(Path, ct);

    public Database Open(string? password = null) => Database.Open(Path, password ?? "pw");

    public async Task<Database> OpenAsync(
        string? password = null,
        CancellationToken ct = default
    ) => await Database.OpenAsync(Path, password ?? "pw", ct: ct);

    public void Dispose()
    {
        Database.Dispose();
        _file.Dispose();
    }
}
