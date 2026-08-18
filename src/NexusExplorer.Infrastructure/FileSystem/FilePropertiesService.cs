using NexusExplorer.Core.Abstractions;

namespace NexusExplorer.Infrastructure.FileSystem;

/// <summary>
/// Implementation of IFilePropertiesService using standard .NET IO.
/// </summary>
public sealed class FilePropertiesService : IFilePropertiesService
{
    public Task<FileProperties> GetPropertiesAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => GetPropertiesInternal(path), cancellationToken);
    }

    private static FileProperties GetPropertiesInternal(string path)
    {
        if (Directory.Exists(path))
        {
            var info = new DirectoryInfo(path);
            return new FileProperties
            {
                Name = info.Name,
                Path = info.FullName,
                DirectoryPath = info.Parent?.FullName ?? info.FullName,
                IsDirectory = true,
                TypeDescription = "Folder",
                Size = 0, // Calculated separately via CalculateDirectorySizeAsync
                Created = info.CreationTime,
                Modified = info.LastWriteTime,
                LastAccessed = info.LastAccessTime,
                IsReadOnly = info.Attributes.HasFlag(FileAttributes.ReadOnly),
                IsHidden = info.Attributes.HasFlag(FileAttributes.Hidden)
            };
        }

        if (File.Exists(path))
        {
            var info = new FileInfo(path);
            var typeDesc = GetFileTypeDescription(info.Extension);
            return new FileProperties
            {
                Name = info.Name,
                Path = info.FullName,
                DirectoryPath = info.DirectoryName ?? "",
                IsDirectory = false,
                TypeDescription = typeDesc,
                Size = info.Length,
                Created = info.CreationTime,
                Modified = info.LastWriteTime,
                LastAccessed = info.LastAccessTime,
                IsReadOnly = info.Attributes.HasFlag(FileAttributes.ReadOnly),
                IsHidden = info.Attributes.HasFlag(FileAttributes.Hidden)
            };
        }

        throw new FileNotFoundException("Path not found.", path);
    }

    public Task<DirectorySizeResult> CalculateDirectorySizeAsync(
        string path,
        IProgress<DirectorySizeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => CalculateDirectorySizeInternal(path, progress, cancellationToken), cancellationToken);
    }

    private static DirectorySizeResult CalculateDirectorySizeInternal(
        string path,
        IProgress<DirectorySizeProgress>? progress,
        CancellationToken cancellationToken)
    {
        long totalSize = 0;
        int fileCount = 0;
        int folderCount = 0;
        int progressCounter = 0;

        var stack = new Stack<string>();
        stack.Push(path);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentDir = stack.Pop();

            try
            {
                var dirInfo = new DirectoryInfo(currentDir);
                if (!dirInfo.Exists) continue;

                foreach (var file in dirInfo.EnumerateFiles())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        totalSize += file.Length;
                        fileCount++;

                        // Report progress periodically
                        if (++progressCounter % 50 == 0)
                        {
                            progress?.Report(new DirectorySizeProgress
                            {
                                CurrentSize = totalSize,
                                FileCount = fileCount,
                                FolderCount = folderCount
                            });
                        }
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }

                foreach (var dir in dirInfo.EnumerateDirectories())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        folderCount++;
                        stack.Push(dir.FullName);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        return new DirectorySizeResult
        {
            TotalSize = totalSize,
            FileCount = fileCount,
            FolderCount = folderCount,
            WasCompleted = true
        };
    }

    public Task SetAttributesAsync(string path, bool isReadOnly, bool isHidden, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(path) && !Directory.Exists(path))
                throw new FileNotFoundException("Path not found.", path);

            var attrs = File.GetAttributes(path);

            // Clear the bits we control
            attrs &= ~FileAttributes.ReadOnly;
            attrs &= ~FileAttributes.Hidden;

            // Set as requested
            if (isReadOnly) attrs |= FileAttributes.ReadOnly;
            if (isHidden) attrs |= FileAttributes.Hidden;

            File.SetAttributes(path, attrs);
        }, cancellationToken);
    }

    private static string GetFileTypeDescription(string? extension)
    {
        if (string.IsNullOrEmpty(extension)) return "File";

        return extension.ToLowerInvariant() switch
        {
            ".cs" => "C# Source File",
            ".js" => "JavaScript File",
            ".ts" => "TypeScript File",
            ".json" => "JSON File",
            ".xml" => "XML File",
            ".html" => "HTML File",
            ".css" => "CSS File",
            ".txt" => "Text File",
            ".md" => "Markdown File",
            ".pdf" => "PDF Document",
            ".doc" or ".docx" => "Word Document",
            ".xls" or ".xlsx" => "Excel Spreadsheet",
            ".png" => "PNG Image",
            ".jpg" or ".jpeg" => "JPEG Image",
            ".gif" => "GIF Image",
            ".svg" => "SVG Image",
            ".zip" => "ZIP Archive",
            ".rar" => "RAR Archive",
            ".7z" => "7-Zip Archive",
            ".exe" => "Executable",
            ".dll" => "Dynamic Link Library",
            ".sln" or ".slnx" => "Visual Studio Solution",
            ".csproj" => "C# Project File",
            ".mp3" => "MP3 Audio",
            ".mp4" => "MP4 Video",
            ".avi" => "AVI Video",
            _ => $"{extension.TrimStart('.').ToUpperInvariant()} File"
        };
    }
}
