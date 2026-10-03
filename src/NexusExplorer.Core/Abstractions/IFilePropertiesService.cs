using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Service for retrieving detailed file/folder properties and metadata.
/// </summary>
public interface IFilePropertiesService
{
    /// <summary>
    /// Gets detailed properties for a single file or directory.
    /// </summary>
    Task<FileProperties> GetPropertiesAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the total size of a directory recursively.
    /// Reports progress via the callback.
    /// </summary>
    Task<DirectorySizeResult> CalculateDirectorySizeAsync(string path, IProgress<DirectorySizeProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets file attributes (Hidden, ReadOnly) on the given path.
    /// </summary>
    Task SetAttributesAsync(string path, bool isReadOnly, bool isHidden, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the "Details" tab metadata (grouped key/value properties) for a file or folder.
    /// Includes generic info everywhere, plus rich media/document/exe metadata where available.
    /// </summary>
    Task<IReadOnlyList<DetailGroup>> GetDetailsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the "Security" tab entries (principal + permissions) for a file or folder.
    /// Read-only. On Windows this reflects the ACL; on other platforms it reflects POSIX
    /// owner/permission info where obtainable, or an empty list when unavailable.
    /// </summary>
    Task<SecurityInfo> GetSecurityAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Detailed properties of a file or directory.
/// </summary>
public sealed class FileProperties
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required string DirectoryPath { get; init; }
    public required bool IsDirectory { get; init; }
    public required string TypeDescription { get; init; }
    public long Size { get; init; }

    /// <summary>Size on disk (allocation size). Equals <see cref="Size"/> when unavailable.</summary>
    public long SizeOnDisk { get; init; }

    public DateTime? Created { get; init; }
    public DateTime? Modified { get; init; }
    public DateTime? LastAccessed { get; init; }
    public bool IsReadOnly { get; init; }
    public bool IsHidden { get; init; }

    /// <summary>System attribute (files marked as OS/system files).</summary>
    public bool IsSystem { get; init; }

    /// <summary>Archive attribute.</summary>
    public bool IsArchive { get; init; }

    /// <summary>Owner (Windows account or POSIX owner) when resolvable; otherwise null.</summary>
    public string? Owner { get; init; }

    /// <summary>The default application that opens this file, when known (Windows).</summary>
    public string? OpensWith { get; init; }

    public int FileCount { get; init; }
    public int FolderCount { get; init; }
}

/// <summary>
/// A named group of detail properties for the "Details" tab (e.g. "File", "Image", "Media").
/// </summary>
public sealed class DetailGroup
{
    public required string Name { get; init; }
    public required IReadOnlyList<DetailProperty> Properties { get; init; }
}

/// <summary>A single key/value row in the "Details" tab.</summary>
public sealed class DetailProperty
{
    public required string Name { get; init; }
    public required string Value { get; init; }
}

/// <summary>
/// Read-only security information for the "Security" tab.
/// </summary>
public sealed class SecurityInfo
{
    public string? Owner { get; init; }

    /// <summary>Access entries (principal + granted permissions). Empty when unavailable.</summary>
    public IReadOnlyList<SecurityEntry> Entries { get; init; } = [];

    /// <summary>Optional note shown when full ACLs aren't available on this platform.</summary>
    public string? Note { get; init; }
}

/// <summary>A single principal and its permissions in the "Security" tab.</summary>
public sealed class SecurityEntry
{
    public required string Principal { get; init; }
    public required string Permissions { get; init; }

    /// <summary>"Allow" or "Deny" (Windows). Null/empty for POSIX-style listings.</summary>
    public string? AccessType { get; init; }
}

/// <summary>
/// Result of a directory size calculation.
/// </summary>
public sealed class DirectorySizeResult
{
    public long TotalSize { get; init; }
    public int FileCount { get; init; }
    public int FolderCount { get; init; }
    public bool WasCompleted { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Progress data for folder size calculation.
/// </summary>
public sealed class DirectorySizeProgress
{
    public long CurrentSize { get; init; }
    public int FileCount { get; init; }
    public int FolderCount { get; init; }
}
