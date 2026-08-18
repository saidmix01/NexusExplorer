namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Abstraction for terminal integration.
/// </summary>
public interface ITerminalService
{
    Task LaunchAsync(string workingDirectory, CancellationToken cancellationToken = default);
}
