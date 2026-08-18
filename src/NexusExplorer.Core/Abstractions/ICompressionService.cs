using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Service for compressing and extracting archive files.
/// </summary>
public interface ICompressionService
{
    /// <summary>
    /// Compresses the specified paths into a ZIP archive.
    /// The archive is created in the parent directory of the first source path.
    /// Uses streaming to avoid loading entire files into memory.
    /// </summary>
    /// <param name="sourcePaths">Files and/or directories to compress.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the created ZIP path on success.</returns>
    Task<CompressionResult> CompressAsync(
        IReadOnlyList<string> sourcePaths,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts a ZIP archive to a destination directory.
    /// If no destination is specified, extracts to a new folder named after the archive
    /// in the same directory as the archive.
    /// </summary>
    /// <param name="archivePath">Path to the ZIP file.</param>
    /// <param name="destinationDirectory">Optional destination. If null, auto-generates from archive name.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the extraction directory on success.</returns>
    Task<CompressionResult> ExtractAsync(
        string archivePath,
        string? destinationDirectory = null,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
