namespace LibKdbx;

/// <summary>
/// KDBX version number (Major.Minor). In the binary format the version field is
/// stored as [Minor LE16][Major LE16].
/// </summary>
public class Version : IEquatable<Version>, IComparable<Version>
{
    public ushort Major { get; }
    public ushort Minor { get; }
    public bool IsZero => Major == 0 && Minor == 0;

    public Version()
    {
        Major = Minor = 0;
    }

    public Version(ushort major, ushort minor)
    {
        Major = major;
        Minor = minor;
    }

    public int CompareTo(Version? other)
    {
        if (other is null)
        {
            return 1;
        }
        if (Major > other.Major)
        {
            return 1;
        }
        if (Major < other.Major)
        {
            return -1;
        }
        if (Minor > other.Minor)
        {
            return 1;
        }
        if (Minor < other.Minor)
        {
            return -1;
        }
        return 0;
    }

    public bool Equals(Version? other) =>
        other is not null && Major == other.Major && Minor == other.Minor;

    public override bool Equals(object? obj) => obj is Version v && Equals(v);

    public override int GetHashCode() => HashCode.Combine(Major, Minor);

    public override string ToString() => $"{Major}.{Minor}";

    /// <summary>Reads a Version from [Minor LE16][Major LE16].</summary>
    public static Version Read(BinaryReader reader)
    {
        ushort minor = reader.ReadUInt16();
        ushort major = reader.ReadUInt16();
        return new Version(major, minor);
    }

    public static Version Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return Read(reader);
    }

    /// <summary>Writes this Version as [Minor LE16][Major LE16].</summary>
    public void Write(BinaryWriter writer)
    {
        writer.Write(Minor);
        writer.Write(Major);
    }

    public void Write(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        Write(writer);
    }

    public static bool operator ==(Version? v1, Version? v2) =>
        v1 is null ? v2 is null : v1.Equals(v2);

    public static bool operator !=(Version? v1, Version? v2) => !(v1 == v2);

    public static bool operator >(Version v1, Version v2) => v1.CompareTo(v2) > 0;
    public static bool operator >=(Version v1, Version v2) => v1.CompareTo(v2) >= 0;
    public static bool operator <(Version v1, Version v2) => v1.CompareTo(v2) < 0;
    public static bool operator <=(Version v1, Version v2) => v1.CompareTo(v2) <= 0;
}
