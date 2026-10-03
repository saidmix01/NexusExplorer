namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Optional platform hook that supplies rich shell metadata for the "Details" tab
/// (image dimensions, media duration, EXE version, document author, etc.).
///
/// The base <see cref="IFilePropertiesService"/> works without it; when a platform
/// registers an implementation (e.g. Windows Property System), the details are enriched.
/// Implementations must be safe to call off the UI thread and return an empty list on failure.
/// </summary>
public interface IShellMetadataProvider
{
    /// <summary>
    /// Returns extra detail groups for the given path (e.g. "Image", "Media", "Origin").
    /// Returns an empty list when no extra metadata is available.
    /// </summary>
    IReadOnlyList<DetailGroup> GetExtraDetails(string path);

    /// <summary>
    /// Returns the friendly name of the default application associated with the file,
    /// or null when unknown/unavailable.
    /// </summary>
    string? GetOpensWith(string path);
}
