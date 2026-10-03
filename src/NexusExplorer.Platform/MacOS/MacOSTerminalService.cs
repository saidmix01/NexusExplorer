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
        // With UseShellExecute=false, .NET on Unix does not strip quotes; use ArgumentList so
        // working directories with spaces are passed as a single argument.
        var psi = new ProcessStartInfo { FileName = "open", UseShellExecute = false };
        psi.ArgumentList.Add("-a");
        psi.ArgumentList.Add("Terminal");
        psi.ArgumentList.Add(workingDirectory);
        Process.Start(psi);
        return Task.CompletedTask;
    }
}
