using System.Diagnostics;
using NexusExplorer.Core.Abstractions;

namespace NexusExplorer.Platform.MacOS;

/// <summary>
/// macOS-specific terminal service that launches Terminal.app.
/// </summary>
public sealed class MacOSTerminalService : ITerminalService
{
    public Task LaunchAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "open",
            Arguments = $"-a Terminal \"{workingDirectory}\"",
            UseShellExecute = false
        });
        return Task.CompletedTask;
    }
}
