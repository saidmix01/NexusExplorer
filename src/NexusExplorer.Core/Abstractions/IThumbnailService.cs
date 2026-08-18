using System.Threading;
using System.Threading.Tasks;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

public interface IThumbnailService
{
    bool CanGenerateThumbnail(FileSystemItem item);
    Task<object?> GetThumbnailAsync(FileSystemItem item, int size, CancellationToken cancellationToken = default);
    void InvalidateCache(FileSystemItem item);
}