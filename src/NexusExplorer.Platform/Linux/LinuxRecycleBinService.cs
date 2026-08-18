using NexusExplorer.Core.Abstractions;

namespace NexusExplorer.Platform.Linux;

/// <summary>
/// Linux Recycle Bin implementation using the FreeDesktop Trash spec.
/// Falls back to permanent delete if trash is not available.
/// </summary>
public sealed class LinuxRecycleBinService : IRecycleBinService
{
    public bool IsSupported => true;

    public Task<bool> RecycleAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                // FreeDesktop Trash: move to ~/.local/share/Trash/files/ with info in Trash/info/
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var trashFiles = Path.Combine(home, ".local", "share", "Trash", "files");
                var trashInfo = Path.Combine(home, ".local", "share", "Trash", "info");

                Directory.CreateDirectory(trashFiles);
                Directory.CreateDirectory(trashInfo);

                var fileName = Path.GetFileName(path);
                var destPath = Path.Combine(trashFiles, fileName);

                // Handle conflicts
                var counter = 1;
                while (File.Exists(destPath) || Directory.Exists(destPath))
                {
                    var name = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    destPath = Path.Combine(trashFiles, $"{name}.{counter}{ext}");
                    counter++;
                }

                // Write .trashinfo file
                var infoPath = Path.Combine(trashInfo, Path.GetFileName(destPath) + ".trashinfo");
                var infoContent = $"[Trash Info]\nPath={Uri.EscapeDataString(path)}\nDeletionDate={DateTime.Now:yyyy-MM-ddTHH:mm:ss}\n";
                File.WriteAllText(infoPath, infoContent);

                // Move to trash
                if (Directory.Exists(path))
                    Directory.Move(path, destPath);
                else
                    File.Move(path, destPath);

                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }
}
