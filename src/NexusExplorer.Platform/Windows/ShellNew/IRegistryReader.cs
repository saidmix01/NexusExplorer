namespace NexusExplorer.Platform.Windows.ShellNew;

/// <summary>
/// A minimal, read-only view over the Windows registry rooted at HKEY_CLASSES_ROOT, scoped to only
/// the operations the ShellNew discovery needs. Abstracting it this way lets the discovery logic
/// (<see cref="ShellNewParser"/>) be unit tested with an in-memory fake instead of the real
/// registry, satisfying the "test without a real registry" requirement while keeping the actual
/// <see cref="WindowsRegistryReader"/> a thin, side-effect-only wrapper.
/// </summary>
/// <remarks>
/// All members must be fail-safe: a missing key or value returns null/empty rather than throwing,
/// so one bad association can never break discovery of the rest.
/// </remarks>
public interface IRegistryReader
{
    /// <summary>
    /// Returns the names of extension subkeys under HKCR that start with a dot (e.g. ".txt",
    /// ".docx"). Order is unspecified.
    /// </summary>
    IReadOnlyList<string> GetExtensionKeys();

    /// <summary>Default (unnamed) value of <c>HKCR\<paramref name="keyPath"/></c>, or null.</summary>
    string? GetDefaultValue(string keyPath);

    /// <summary>Named string value under <c>HKCR\<paramref name="keyPath"/></c>, or null.</summary>
    string? GetStringValue(string keyPath, string valueName);

    /// <summary>Named binary value under <c>HKCR\<paramref name="keyPath"/></c>, or null.</summary>
    byte[]? GetBinaryValue(string keyPath, string valueName);

    /// <summary>True if <c>HKCR\<paramref name="keyPath"/></c> exists.</summary>
    bool KeyExists(string keyPath);

    /// <summary>Names of the values present under <c>HKCR\<paramref name="keyPath"/></c>.</summary>
    IReadOnlyList<string> GetValueNames(string keyPath);

    /// <summary>Names of the immediate subkeys of <c>HKCR\<paramref name="keyPath"/></c>.</summary>
    IReadOnlyList<string> GetSubKeyNames(string keyPath);
}
