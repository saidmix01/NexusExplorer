using NexusExplorer.Core.Abstractions;

namespace NexusExplorer.Platform;

/// <summary>
/// No-op <see cref="IRecycleBinQueryService"/> for platforms that don't support
/// enumerating/restoring the trash through this app. Reports unsupported and returns empty.
/// </summary>
public sealed class UnsupportedRecycleBinQueryService : IRecycleBinQueryService
{
    public bool IsSupported => false;

    public Task<IReadOnlyList<RecycleBinEntry>> EnumerateAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RecycleBinEntry>>([]);

    public Task<bool> RestoreAsync(string originalPath, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> EmptyAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
