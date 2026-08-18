using System.Diagnostics;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Platform;

/// <summary>
/// Cross-platform implementation of ISendToService.
/// Provides basic targets: Desktop folder and Downloads folder.
/// Uses xdg-open (Linux) or 'open' (macOS) for default app opening.
/// </summary>
public sealed class DefaultSendToService : ISendToService
{
    public IReadOnlyList<SendToTarget> GetTargets()
    {
        var targets = new List<SendToTarget>();

        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (!string.IsNullOrEmpty(desktopPath) && Directory.Exists(desktopPath))
        {
            targets.Add(new SendToTarget { Name = "Desktop", Path = desktopPath });
        }

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrEmpty(documentsPath) && Directory.Exists(documentsPath))
        {
            targets.Add(new SendToTarget { Name = "Documents", Path = documentsPath });
        }

        var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(downloadsPath))
        {
            targets.Add(new SendToTarget { Name = "Downloads", Path = downloadsPath });
        }

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

        if (string.IsNullOrEmpty(target.Path) || !Directory.Exists(target.Path))
            return FileOperationResult.Failed($"Target directory '{target.Name}' does not exist.");

        try
        {
            var itemsProcessed = 0;
            var totalItems = sourcePaths.Count;

            foreach (var source in sourcePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileName = Path.GetFileName(source);
                var destPath = Path.Combine(target.Path, fileName);

                // Handle conflict
                if (File.Exists(destPath) || Directory.Exists(destPath))
                {
                    if (conflictResolver is not null)
                    {
                        var conflict = new FileConflict
                        {
                            SourcePath = source,
                            DestinationPath = destPath,
                            FileName = fileName
                        };
                        var action = await conflictResolver(conflict);
                        if (action == ConflictAction.Skip) { itemsProcessed++; continue; }
                        if (action == ConflictAction.RenameAutomatically)
                        {
                            var name = Path.GetFileNameWithoutExtension(fileName);
                            var ext = Path.GetExtension(fileName);
                            var counter = 1;
                            do
                            {
                                destPath = Path.Combine(target.Path, $"{name} ({counter}){ext}");
                                counter++;
                            } while (File.Exists(destPath) || Directory.Exists(destPath));
                        }
                    }
                }

                if (Directory.Exists(source))
                    CopyDirectory(source, destPath);
                else if (File.Exists(source))
                    File.Copy(source, destPath, overwrite: true);

                itemsProcessed++;
                progress?.Report(new FileOperationProgress
                {
                    OperationType = FileOperationType.Copy,
                    CurrentItem = fileName,
                    CurrentItemIndex = itemsProcessed,
                    TotalItems = totalItems,
                    IsCompleted = itemsProcessed == totalItems
                });
            }

            return new FileOperationResult { Success = true, ItemsProcessed = itemsProcessed };
        }
        catch (OperationCanceledException)
        {
            return new FileOperationResult { Success = false, Cancelled = true };
        }
        catch (Exception ex)
        {
            return FileOperationResult.Failed(ex.Message);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }
}
