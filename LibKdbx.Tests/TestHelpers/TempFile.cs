namespace LibKdbx.Tests;

/// <summary>
/// Disposable temp file that is deleted on <see cref="Dispose"/>.
/// </summary>
public sealed class TempFile : IDisposable
{
    public string Path { get; }

    public TempFile()
    {
        Path = System.IO.Path.GetTempFileName();
    }

    public void WriteAllBytes(byte[] data) => File.WriteAllBytes(Path, data);

    public void WriteAllText(string text) => File.WriteAllText(Path, text);

    public byte[] ReadAllBytes() => File.ReadAllBytes(Path);

    public string ReadAllText() => File.ReadAllText(Path);

    public void Dispose()
    {
        try
        {
            File.Delete(Path);
        }
        catch
        { /* best effort */
        }
    }
}
