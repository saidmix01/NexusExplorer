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
                // Use osascript to move to Trash. With UseShellExecute=false there is no shell,
                // so we must pass the script as a discrete argument via ArgumentList (NOT a quoted
                // Arguments string — .NET on Unix does not interpret quotes).
                var escapedPath = path.Replace("\"", "\\\"");
                var script = $"tell application \"Finder\" to delete POSIX file \"{escapedPath}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/osascript",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                psi.ArgumentList.Add("-e");
                psi.ArgumentList.Add(script);

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
