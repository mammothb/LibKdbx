namespace Mmb.KeePass;

/// <summary>
/// Three-state boolean for Group flags (EnableAutoType, EnableSearching).
/// Mimics KeePassXC's Group::TriState.
/// </summary>
public enum TriState
{
    /// <summary>Inherit value from parent group (written as "null" in XML).</summary>
    Inherit,

    /// <summary>Force enabled (written as "True" in XML).</summary>
    Enable,

    /// <summary>Force disabled (written as "False" in XML).</summary>
    Disable
}
