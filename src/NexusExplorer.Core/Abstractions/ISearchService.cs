using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

public interface ISearchService
{
    IAsyncEnumerable<FileSystemItem> SearchAsync(string directoryPath, string query, CancellationToken cancellationToken = default);
}
