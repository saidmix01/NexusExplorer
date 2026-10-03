using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NexusExplorer.Core.Abstractions;

namespace NexusExplorer.Infrastructure.FileSystem;

/// <summary>
/// Implementation of IFilePropertiesService using standard .NET IO, with optional
/// platform enrichment for the "Details" tab via an <see cref="IShellMetadataProvider"/>.
/// Everything degrades gracefully so it works on Windows, Linux and macOS.
/// </summary>
public sealed class FilePropertiesService : IFilePropertiesService
{
    private readonly IShellMetadataProvider? _shellMetadata;

    public FilePropertiesService(IShellMetadataProvider? shellMetadata = null)
    {
        _shellMetadata = shellMetadata;
    }

    public Task<FileProperties> GetPropertiesAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => GetPropertiesInternal(path), cancellationToken);
    }

    private FileProperties GetPropertiesInternal(string path)
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
                SizeOnDisk = 0,
                Created = info.CreationTime,
                Modified = info.LastWriteTime,
                LastAccessed = info.LastAccessTime,
                IsReadOnly = info.Attributes.HasFlag(FileAttributes.ReadOnly),
                IsHidden = info.Attributes.HasFlag(FileAttributes.Hidden),
                IsSystem = info.Attributes.HasFlag(FileAttributes.System),
                IsArchive = info.Attributes.HasFlag(FileAttributes.Archive),
                Owner = TryGetOwner(path)
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
                SizeOnDisk = GetSizeOnDisk(info.FullName, info.Length),
                Created = info.CreationTime,
                Modified = info.LastWriteTime,
                LastAccessed = info.LastAccessTime,
                IsReadOnly = info.Attributes.HasFlag(FileAttributes.ReadOnly),
                IsHidden = info.Attributes.HasFlag(FileAttributes.Hidden),
                IsSystem = info.Attributes.HasFlag(FileAttributes.System),
                IsArchive = info.Attributes.HasFlag(FileAttributes.Archive),
                Owner = TryGetOwner(path),
                OpensWith = _shellMetadata?.GetOpensWith(path)
            };
        }

        throw new FileNotFoundException("Path not found.", path);
    }

    public Task<IReadOnlyList<DetailGroup>> GetDetailsAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<DetailGroup>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var groups = new List<DetailGroup>();

            var isDir = Directory.Exists(path);
            var isFile = File.Exists(path);
            if (!isDir && !isFile) return groups;

            // --- File group (generic, all platforms) ---
            var fileProps = new List<DetailProperty>();
            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            fileProps.Add(new DetailProperty { Name = "Name", Value = name });

            if (isFile)
            {
                var fi = new FileInfo(path);
                fileProps.Add(new DetailProperty { Name = "Item type", Value = GetFileTypeDescription(fi.Extension) });
                fileProps.Add(new DetailProperty { Name = "Folder path", Value = fi.DirectoryName ?? "" });
                fileProps.Add(new DetailProperty { Name = "Size", Value = FormatSize(fi.Length) });
                fileProps.Add(new DetailProperty { Name = "Date created", Value = FormatDate(fi.CreationTime) });
                fileProps.Add(new DetailProperty { Name = "Date modified", Value = FormatDate(fi.LastWriteTime) });
                fileProps.Add(new DetailProperty { Name = "Date accessed", Value = FormatDate(fi.LastAccessTime) });
                fileProps.Add(new DetailProperty { Name = "Attributes", Value = DescribeAttributes(fi.Attributes) });
            }
            else
            {
                var di = new DirectoryInfo(path);
                fileProps.Add(new DetailProperty { Name = "Item type", Value = "Folder" });
                fileProps.Add(new DetailProperty { Name = "Folder path", Value = di.Parent?.FullName ?? di.FullName });
                fileProps.Add(new DetailProperty { Name = "Date created", Value = FormatDate(di.CreationTime) });
                fileProps.Add(new DetailProperty { Name = "Date modified", Value = FormatDate(di.LastWriteTime) });
                fileProps.Add(new DetailProperty { Name = "Attributes", Value = DescribeAttributes(di.Attributes) });
            }

            var owner = TryGetOwner(path);
            if (!string.IsNullOrEmpty(owner))
                fileProps.Add(new DetailProperty { Name = "Owner", Value = owner });

            groups.Add(new DetailGroup { Name = "File", Properties = fileProps });

            // --- Platform-enriched groups (Windows Property System, etc.) ---
            if (_shellMetadata is not null && isFile)
            {
                try
                {
                    var extra = _shellMetadata.GetExtraDetails(path);
                    foreach (var g in extra)
                        if (g.Properties.Count > 0)
                            groups.Add(g);
                }
                catch
                {
                    // Enrichment is best-effort; ignore provider failures.
                }
            }

            return groups;
        }, cancellationToken);
    }

    public Task<SecurityInfo> GetSecurityAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (OperatingSystem.IsWindows())
                return GetWindowsSecurity(path);

            // Non-Windows: expose the POSIX owner and a note (full ACL editing isn't offered).
            var owner = TryGetOwner(path);
            return new SecurityInfo
            {
                Owner = owner,
                Entries = [],
                Note = "Detailed per-user permissions are only available on Windows."
            };
        }, cancellationToken);
    }

    [SupportedOSPlatform("windows")]
    private static SecurityInfo GetWindowsSecurity(string path)
    {
        try
        {
            var isDir = Directory.Exists(path);
            System.Security.AccessControl.FileSystemSecurity security = isDir
                ? new DirectoryInfo(path).GetAccessControl()
                : new FileInfo(path).GetAccessControl();

            string? owner = null;
            try
            {
                owner = security.GetOwner(typeof(System.Security.Principal.NTAccount))?.Value;
            }
            catch { /* owner may be unresolvable */ }

            var entries = new List<SecurityEntry>();
            var rules = security.GetAccessRules(true, true, typeof(System.Security.Principal.NTAccount));
            foreach (System.Security.AccessControl.FileSystemAccessRule rule in rules)
            {
                entries.Add(new SecurityEntry
                {
                    Principal = rule.IdentityReference.Value,
                    Permissions = DescribeRights(rule.FileSystemRights),
                    AccessType = rule.AccessControlType.ToString() // Allow / Deny
                });
            }

            return new SecurityInfo { Owner = owner, Entries = entries };
        }
        catch (Exception ex)
        {
            return new SecurityInfo { Note = $"Unable to read permissions: {ex.Message}" };
        }
    }

    [SupportedOSPlatform("windows")]
    private static string DescribeRights(System.Security.AccessControl.FileSystemRights rights)
    {
        // Collapse to the friendly buckets Windows shows.
        var parts = new List<string>();
        if (rights.HasFlag(System.Security.AccessControl.FileSystemRights.FullControl))
            return "Full control";
        if ((rights & System.Security.AccessControl.FileSystemRights.Modify) == System.Security.AccessControl.FileSystemRights.Modify)
            parts.Add("Modify");
        if ((rights & System.Security.AccessControl.FileSystemRights.ReadAndExecute) == System.Security.AccessControl.FileSystemRights.ReadAndExecute)
            parts.Add("Read & execute");
        else if (rights.HasFlag(System.Security.AccessControl.FileSystemRights.Read))
            parts.Add("Read");
        if (rights.HasFlag(System.Security.AccessControl.FileSystemRights.Write))
            parts.Add("Write");

        return parts.Count > 0 ? string.Join(", ", parts) : "Special";
    }

    private static string? TryGetOwner(string path)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            System.Security.AccessControl.FileSystemSecurity security = Directory.Exists(path)
                ? new DirectoryInfo(path).GetAccessControl()
                : new FileInfo(path).GetAccessControl();
            return security.GetOwner(typeof(System.Security.Principal.NTAccount))?.Value;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the size on disk (allocation size). On Windows uses GetCompressedFileSizeW;
    /// on other platforms rounds up to a typical 4 KB block as a reasonable approximation.
    /// </summary>
    private static long GetSizeOnDisk(string path, long logicalSize)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                uint low = GetCompressedFileSizeW(path, out uint high);
                if (low != 0xFFFFFFFF || Marshal.GetLastWin32Error() == 0)
                    return ((long)high << 32) | low;
            }
            catch
            {
                // Fall through to the block-rounding estimate.
            }
        }

        const long block = 4096;
        return logicalSize == 0 ? 0 : ((logicalSize + block - 1) / block) * block;
    }

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetCompressedFileSizeW(string lpFileName, out uint lpFileSizeHigh);

    private static string DescribeAttributes(FileAttributes attrs)
    {
        var parts = new List<string>();
        if (attrs.HasFlag(FileAttributes.ReadOnly)) parts.Add("Read-only");
        if (attrs.HasFlag(FileAttributes.Hidden)) parts.Add("Hidden");
        if (attrs.HasFlag(FileAttributes.System)) parts.Add("System");
        if (attrs.HasFlag(FileAttributes.Archive)) parts.Add("Archive");
        if (attrs.HasFlag(FileAttributes.Compressed)) parts.Add("Compressed");
        if (attrs.HasFlag(FileAttributes.Encrypted)) parts.Add("Encrypted");
        if (attrs.HasFlag(FileAttributes.ReparsePoint)) parts.Add("Reparse point");
        return parts.Count > 0 ? string.Join(", ", parts) : "Normal";
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 bytes";
        string[] units = ["bytes", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return unit == 0 ? $"{bytes} bytes" : $"{value:0.##} {units[unit]} ({bytes:N0} bytes)";
    }

    private static string FormatDate(DateTime date)
        => date.ToString("dddd, MMMM d, yyyy h:mm:ss tt", System.Globalization.CultureInfo.CurrentCulture);

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
