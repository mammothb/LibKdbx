namespace LibKdbx;

/// <summary>
/// A single key-value pair in CustomData, with an optional last-modified timestamp.
/// </summary>
public readonly record struct CustomDataItem(string Value, DateTime? LastModified = null);
