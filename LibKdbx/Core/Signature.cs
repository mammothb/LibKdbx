namespace LibKdbx;

/// <summary>
/// KDBX file magic signature (two 32-bit words).
/// </summary>
public sealed class Signature : IEquatable<Signature>
{
    public uint Sign1 { get; }
    public uint Sign2 { get; }
    public bool IsZero => Sign1 == 0 && Sign2 == 0;

    public Signature()
    {
        Sign1 = Sign2 = 0;
    }

    public Signature(uint sign1, uint sign2)
    {
        Sign1 = sign1;
        Sign2 = sign2;
    }

    public bool Equals(Signature? other) =>
        other is not null && Sign1 == other.Sign1 && Sign2 == other.Sign2;

    public override bool Equals(object? obj) => obj is Signature s && Equals(s);

    public override int GetHashCode() => HashCode.Combine(Sign1, Sign2);

    public static Signature Read(BinaryReader reader) =>
        new(reader.ReadUInt32(), reader.ReadUInt32());

    public static Signature Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return Read(reader);
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(Sign1);
        writer.Write(Sign2);
    }

    public void Write(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        Write(writer);
    }

    public static bool operator ==(Signature? s1, Signature? s2) =>
        s1 is null ? s2 is null : s1.Equals(s2);

    public static bool operator !=(Signature? s1, Signature? s2) => !(s1 == s2);
}
