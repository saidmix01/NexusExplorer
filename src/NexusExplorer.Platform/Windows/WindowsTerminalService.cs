using System.Diagnostics;
using NexusExplorer.Core.Abstractions;

namespace NexusExplorer.Platform.Windows;

/// <summary>
/// Windows-specific terminal service that launches Windows Terminal or cmd.
/// </summary>
public sealed class WindowsTerminalService : ITerminalService
{
    public Task LaunchAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "wt.exe",
            Arguments = $"-d \"{workingDirectory}\"",
            UseShellExecute = true
        });
        return Task.CompletedTask;
    }
}
