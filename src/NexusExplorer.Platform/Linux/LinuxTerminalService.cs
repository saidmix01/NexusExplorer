using System.Diagnostics;
using NexusExplorer.Core.Abstractions;

namespace NexusExplorer.Platform.Linux;

/// <summary>
/// Linux-specific terminal service.
/// </summary>
public sealed class LinuxTerminalService : ITerminalService
{
    public Task LaunchAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "x-terminal-emulator",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        });
        return Task.CompletedTask;
    }
}
