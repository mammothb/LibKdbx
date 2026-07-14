namespace LibKdbx;

/// <summary>
/// Abstraction over the KDBX binary header (V3 and V4).
/// </summary>
public interface IHeader
{
    Signature Signature { get; }
    Version Version { get; }
    Guid CipherId { get; }
    bool IsVersion4 { get; }
    bool IsCompressed { get; }
    byte[] MasterSeed { get; }

    /// <summary>KDBX 3.x only — null for KDBX 4.x.</summary>
    byte[]? StreamStartBytes { get; }

    /// <summary>
    /// Inner protected-stream algorithm.
    /// V3: stored in the outer header (InnerRandomStreamId field).
    /// V4: always ChaCha20 — the actual value is in the inner header.
    /// </summary>
    ProtectedStreamAlgorithm InnerStreamAlgorithm { get; }

    IKdf CreateKdf();
    SymmetricCipher CreateCipher(byte[] key);

    /// <summary>
    /// Patches the outer-header inner-stream fields (V3 only).
    /// Must be called before Write() when reusing an existing header with a fresh ProtectedStream.
    /// </summary>
    void SetInnerStream(ProtectedStreamAlgorithm algorithm, byte[] key);

    void Write(BinaryWriter writer);
    void Write(Stream stream);
}
