namespace LibKdbx;

/// <summary>
/// Key Derivation Function interface.
/// Transforms the 32-byte composite key into the 32-byte derived key.
/// </summary>
public interface IKdf
{
    byte[] Transform(byte[] rawKey);
    VariantMap Parameters();
}
