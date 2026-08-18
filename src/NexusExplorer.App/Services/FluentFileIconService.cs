using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.Services;

/// <summary>
/// Resolves icons for FileSystemItems using the centralized FluentIconProvider.
/// Considers known filenames, extensions, and item type.
/// </summary>
public sealed class FluentFileIconService : IFileIconService
{
    private readonly FluentIconProvider _iconProvider;

    public FluentFileIconService(IIconProvider iconProvider)
    {
        // We need the concrete type for GetFileIconForItem
        _iconProvider = (FluentIconProvider)iconProvider;
    }

    public object GetIcon(FileSystemItem item)
    {
        return _iconProvider.GetFileIconForItem(item);
    }
}
