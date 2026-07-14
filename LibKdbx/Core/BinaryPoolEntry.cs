namespace LibKdbx;

/// <summary>
/// A single entry in the KDBX binary pool: attachment data and its protection flag.
/// <c>IsProtected</c> is true when the binary value in the inner header has the
/// protection bit set. Currently always false for LibKdbx-authored files, but
/// preserved round-trip for interop with other KDBX implementations.
/// </summary>
public readonly record struct BinaryPoolEntry(bool IsProtected, byte[] Data);
