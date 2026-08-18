using System.Diagnostics;
using NexusExplorer.Core.Abstractions;

namespace NexusExplorer.Platform.MacOS;

/// <summary>
/// macOS Recycle Bin implementation using AppleScript/osascript.
/// </summary>
public sealed class MacOSRecycleBinService : IRecycleBinService
{
    public bool IsSupported => true;

    public Task<bool> RecycleAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                // Use osascript to move to Trash
                var escapedPath = path.Replace("\"", "\\\"");
                var script = $"tell application \"Finder\" to delete POSIX file \"{escapedPath}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/osascript",
                    Arguments = $"-e '{script}'",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit(5000);
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }
}
