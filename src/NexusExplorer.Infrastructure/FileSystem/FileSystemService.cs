using Microsoft.Extensions.Logging;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Infrastructure.FileSystem;

/// <summary>
/// Implementation of file system operations using System.IO.
/// Results are sorted: directories first, then files, both alphabetically by name.
/// </summary>
public sealed class FileSystemService : IFileSystemService
{
    private readonly ILogger<FileSystemService> _logger;

    public FileSystemService(ILogger<FileSystemService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<FileSystemItem>> GetItemsAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => GetItemsInternal(path, cancellationToken), cancellationToken);
    }

    private IReadOnlyList<FileSystemItem> GetItemsInternal(string path, CancellationToken cancellationToken)
    {
        var directories = new List<FileSystemItem>();
        var files = new List<FileSystemItem>();

        try
        {
            var dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                _logger.LogWarning("Directory does not exist: {Path}", path);
                return Array.Empty<FileSystemItem>();
            }

            foreach (var dir in dirInfo.EnumerateDirectories())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    directories.Add(MapDirectory(dir));
                }
                catch (UnauthorizedAccessException)
                {
                    _logger.LogDebug("Access denied to directory: {Path}", dir.FullName);
                }
                catch (IOException ex)
                {
                    _logger.LogDebug(ex, "IO error reading directory: {Path}", dir.FullName);
                }
            }

            foreach (var file in dirInfo.EnumerateFiles())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    files.Add(MapFile(file));
                }
                catch (UnauthorizedAccessException)
                {
                    _logger.LogDebug("Access denied to file: {Path}", file.FullName);
                }
                catch (IOException ex)
                {
                    _logger.LogDebug(ex, "IO error reading file: {Path}", file.FullName);
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied to directory: {Path}", path);
            throw;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogWarning(ex, "Directory not found: {Path}", path);
            throw;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error reading directory: {Path}", path);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected error reading directory: {Path}", path);
            throw;
        }

        // Sort: directories first alphabetically, then files alphabetically
        directories.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        files.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        var result = new List<FileSystemItem>(directories.Count + files.Count);
        result.AddRange(directories);
        result.AddRange(files);
        return result.AsReadOnly();
    }

    public Task<FileSystemItem?> GetItemAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            if (Directory.Exists(path))
            {
                var dirInfo = new DirectoryInfo(path);
                return Task.FromResult<FileSystemItem?>(MapDirectory(dirInfo));
            }

            if (File.Exists(path))
            {
                var fileInfo = new FileInfo(path);
                return Task.FromResult<FileSystemItem?>(MapFile(fileInfo));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error getting item info: {Path}", path);
        }

        return Task.FromResult<FileSystemItem?>(null);
    }

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Directory.Exists(path) || File.Exists(path));
    }

    public Task<IReadOnlyList<NavigationItem>> GetDrivesAsync(CancellationToken cancellationToken = default)
    {
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => new NavigationItem
            {
                Name = string.IsNullOrWhiteSpace(d.VolumeLabel)
                    ? d.Name
                    : $"{d.VolumeLabel} ({d.Name.TrimEnd(Path.DirectorySeparatorChar)})",
                Path = d.RootDirectory.FullName,
                Kind = d.DriveType == DriveType.Network ? NavigationItemKind.Network : NavigationItemKind.Drive,
                Section = d.DriveType == DriveType.Network ? NavigationSection.Network : NavigationSection.Locations
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<NavigationItem>>(drives.AsReadOnly());
    }

    internal static FileSystemItem MapDirectory(DirectoryInfo dir) => new()
    {
        Name = dir.Name,
        Path = dir.FullName,
        Type = dir.Attributes.HasFlag(FileAttributes.ReparsePoint)
            ? FileSystemItemType.SymbolicLink
            : FileSystemItemType.Directory,
        LastModified = dir.LastWriteTime,
        Created = dir.CreationTime,
        IsHidden = dir.Attributes.HasFlag(FileAttributes.Hidden),
        IsReadOnly = dir.Attributes.HasFlag(FileAttributes.ReadOnly)
    };

    internal static FileSystemItem MapFile(FileInfo file) => new()
    {
        Name = file.Name,
        Path = file.FullName,
        Type = file.Attributes.HasFlag(FileAttributes.ReparsePoint)
            ? FileSystemItemType.SymbolicLink
            : FileSystemItemType.File,
        Size = file.Length,
        LastModified = file.LastWriteTime,
        Created = file.CreationTime,
        Extension = file.Extension,
        IsHidden = file.Attributes.HasFlag(FileAttributes.Hidden),
        IsReadOnly = file.Attributes.HasFlag(FileAttributes.ReadOnly)
    };

    public Task<IReadOnlyList<DriveItem>> GetDriveItemsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var drives = DriveInfo.GetDrives()
                .Select(d =>
                {
                    try
                    {
                        var category = d.DriveType switch
                        {
                            DriveType.Fixed => DriveCategory.Fixed,
                            DriveType.Removable => DriveCategory.Removable,
                            DriveType.Network => DriveCategory.Network,
                            DriveType.CDRom => DriveCategory.Optical,
                            DriveType.Ram => DriveCategory.Ram,
                            _ => DriveCategory.Unknown
                        };

                        var letter = d.Name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                        if (!d.IsReady)
                        {
                            return new DriveItem
                            {
                                Name = $"{letter}",
                                Path = d.RootDirectory.FullName,
                                Category = category,
                                DriveLetter = letter,
                                IsReady = false
                            };
                        }

                        var label = string.IsNullOrWhiteSpace(d.VolumeLabel)
                            ? (category == DriveCategory.Network ? "Network Drive" : "Local Disk")
                            : d.VolumeLabel;

                        return new DriveItem
                        {
                            Name = $"{label} ({letter})",
                            Path = d.RootDirectory.FullName,
                            Category = category,
                            TotalSize = d.TotalSize,
                            FreeSpace = d.AvailableFreeSpace,
                            VolumeLabel = d.VolumeLabel,
                            DriveLetter = letter,
                            IsReady = true
                        };
                    }
                    catch
                    {
                        // Drive not accessible
                        var letter = d.Name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        return new DriveItem
                        {
                            Name = $"{letter}",
                            Path = d.RootDirectory.FullName,
                            Category = DriveCategory.Unknown,
                            DriveLetter = letter,
                            IsReady = false
                        };
                    }
                })
                .ToList();

            return (IReadOnlyList<DriveItem>)drives.AsReadOnly();
        }, cancellationToken);
    }

    public Task<int?> GetDirectoryItemCountAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run<int?>(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Directory.Exists(path)) return null;

                var dir = new DirectoryInfo(path);
                return dir.EnumerateFileSystemInfos().Take(100_000).Count();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }, cancellationToken);
    }
}
