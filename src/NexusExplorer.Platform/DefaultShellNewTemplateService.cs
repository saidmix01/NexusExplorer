using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Platform;

/// <summary>
/// Cross-platform fallback for <see cref="IShellNewTemplateService"/> (Linux/macOS, or any host
/// without a Windows-style ShellNew registry). It offers only the generic options Nexus has always
/// provided — a folder and a plain text document — so the "New" submenu still works everywhere.
/// </summary>
public sealed class DefaultShellNewTemplateService : IShellNewTemplateService
{
    private static readonly IReadOnlyList<NewItemDefinition> Items =
    [
        new NewItemDefinition
        {
            DisplayName = "Folder",
            Kind = NewItemKind.Folder,
            DefaultBaseName = "New Folder",
            Source = NewItemSource.BuiltIn,
        },
        new NewItemDefinition
        {
            DisplayName = "Text Document",
            Kind = NewItemKind.EmptyFile,
            Extension = ".txt",
            DefaultBaseName = "New Text Document",
            Source = NewItemSource.BuiltIn,
        },
    ];

    public Task<IReadOnlyList<NewItemDefinition>> GetNewItemsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Items);

    public void InvalidateCache()
    {
        // Nothing to cache: the generic set is static.
    }
}
