using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Platform.Windows;

/// <summary>
/// Windows implementation of ISendToService.
/// Reads targets from the system SendTo folder (%APPDATA%\Microsoft\Windows\SendTo).
/// </summary>
public sealed class WindowsSendToService : ISendToService
{
    private readonly IFileOperationService _fileOperationService;
    private readonly ICompressionService _compressionService;

    public WindowsSendToService(IFileOperationService fileOperationService, ICompressionService compressionService)
    {
        _fileOperationService = fileOperationService;
        _compressionService = compressionService;
    }

    public IReadOnlyList<SendToTarget> GetTargets()
    {
        var targets = new List<SendToTarget>();

        // Read the Windows SendTo folder
        var sendToFolder = GetSendToFolderPath();
        if (sendToFolder is null || !Directory.Exists(sendToFolder))
            return targets;

        foreach (var entry in Directory.EnumerateFileSystemEntries(sendToFolder))
        {
            var target = ResolveTarget(entry);
            if (target is not null)
                targets.Add(target);
        }

        // Sort alphabetically
        targets.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        return targets;
    }

    public async Task<FileOperationResult> SendToAsync(
        IReadOnlyList<string> sourcePaths,
        SendToTarget target,
        IProgress<FileOperationProgress>? progress = null,
        Func<FileConflict, Task<ConflictAction>>? conflictResolver = null,
        CancellationToken cancellationToken = default)
    {
        if (sourcePaths.Count == 0)
            return FileOperationResult.Failed("No items selected.");

        if (target.Type == SendToTargetType.CompressedFolder)
        {
            // Use compression service
            var result = await _compressionService.CompressAsync(sourcePaths, progress, cancellationToken);
            if (result.Success)
                return FileOperationResult.Ok(result.ItemsProcessed);
            if (result.Cancelled)
                return FileOperationResult.CancelledResult();
            return FileOperationResult.Failed(result.Error ?? "Compression failed.");
        }

        if (target.Type == SendToTargetType.Folder && target.Path is not null)
        {
            // Use existing copy operation to copy files to the target folder
            return await _fileOperationService.CopyAsync(
                sourcePaths, target.Path, progress, conflictResolver, cancellationToken);
        }

        return FileOperationResult.Failed($"Unsupported Send To target: {target.Name}");
    }

    internal static string? GetSendToFolderPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appData))
            return null;

        return Path.Combine(appData, "Microsoft", "Windows", "SendTo");
    }

    private static SendToTarget? ResolveTarget(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(name))
            return null;

        // Check if it's a directory shortcut or actual folder
        if (Directory.Exists(path))
        {
            return new SendToTarget
            {
                Name = name,
                Path = path,
                Type = SendToTargetType.Folder
            };
        }

        // Handle .lnk shortcuts
        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var resolvedPath = ResolveShortcut(path);
            if (resolvedPath is not null && Directory.Exists(resolvedPath))
            {
                return new SendToTarget
                {
                    Name = name,
                    Path = resolvedPath,
                    Type = SendToTargetType.Folder
                };
            }

            // "Compressed (zipped) Folder" is a special shell extension
            if (name.Contains("Compressed", StringComparison.OrdinalIgnoreCase)
                || name.Contains("zipped", StringComparison.OrdinalIgnoreCase)
                || name.Contains("zip", StringComparison.OrdinalIgnoreCase))
            {
                return new SendToTarget
                {
                    Name = name,
                    Path = null,
                    Type = SendToTargetType.CompressedFolder
                };
            }

            // For other shortcuts that point to non-folder targets, skip them
            return null;
        }

        // Skip non-shortcut files (e.g., .mapimail, .url)
        // We only support folder destinations and compressed folder
        return null;
    }

    private static string? ResolveShortcut(string lnkPath)
    {
        // Simple shortcut resolution using COM Shell
        // For robustness, we try to read the .lnk file target
        try
        {
            // Use Windows Script Host COM object through shell
            // Fallback: check common known targets by name
            var name = Path.GetFileNameWithoutExtension(lnkPath);

            // Map well-known SendTo names to special folders
            if (name is not null)
            {
                if (name.Equals("Desktop", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Desktop", StringComparison.OrdinalIgnoreCase))
                {
                    return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                }

                if (name.Equals("Documents", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Documents", StringComparison.OrdinalIgnoreCase))
                {
                    return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }

                if (name.Contains("Mail", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Fax", StringComparison.OrdinalIgnoreCase))
                {
                    // These are not folder targets
                    return null;
                }
            }

            // Try to read .lnk binary to extract the target path
            return ReadLnkTarget(lnkPath);
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadLnkTarget(string lnkPath)
    {
        // Minimal .lnk parser - reads the target path from a Windows shell link file.
        // The .lnk format starts with a header, then optional sections.
        try
        {
            using var stream = new FileStream(lnkPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream);

            // Header size must be 0x4C
            var headerSize = reader.ReadInt32();
            if (headerSize != 0x4C) return null;

            // Skip CLSID (16 bytes)
            reader.ReadBytes(16);

            // Link flags
            var flags = reader.ReadUInt32();
            var hasLinkTargetIdList = (flags & 0x01) != 0;
            var hasLinkInfo = (flags & 0x02) != 0;

            // Skip file attributes, timestamps, file size, icon index, show command, hotkey, reserved
            reader.ReadBytes(40);

            // Skip LinkTargetIDList if present
            if (hasLinkTargetIdList)
            {
                var idListSize = reader.ReadUInt16();
                reader.ReadBytes(idListSize);
            }

            // Read LinkInfo to get the local path
            if (hasLinkInfo)
            {
                var linkInfoStartPos = stream.Position;
                var linkInfoSize = reader.ReadInt32();
                var linkInfoHeaderSize = reader.ReadInt32();
                var linkInfoFlags = reader.ReadInt32();
                var localBasePathOffset = reader.ReadInt32();

                if ((linkInfoFlags & 0x01) != 0 && localBasePathOffset > 0) // VolumeIDAndLocalBasePath
                {
                    stream.Position = linkInfoStartPos + localBasePathOffset;
                    var pathBytes = new List<byte>();
                    byte b;
                    while ((b = reader.ReadByte()) != 0)
                        pathBytes.Add(b);

                    var localPath = DecodeAnsi(pathBytes.ToArray());
                    if (!string.IsNullOrEmpty(localPath))
                        return localPath;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Decodes the ANSI LocalBasePath of a .lnk. On modern .NET, Encoding.Default is UTF-8
    /// (not the system ANSI code page), which mis-decodes non-ASCII bytes in the ANSI path.
    /// Latin1 is built in and maps every byte 1:1, which round-trips ASCII correctly and
    /// avoids the UTF-8 mis-decoding without pulling in the code-pages package.
    /// </summary>
    private static string DecodeAnsi(byte[] bytes) => System.Text.Encoding.Latin1.GetString(bytes);
}
