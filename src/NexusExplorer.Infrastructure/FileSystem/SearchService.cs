using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Infrastructure.FileSystem;

public class SearchService : ISearchService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly ILogger<SearchService> _logger;

    public SearchService(IFileSystemService fileSystemService, ILogger<SearchService>? logger = null)
    {
        _fileSystemService = fileSystemService;
        _logger = logger ?? NullLogger<SearchService>.Instance;
    }

    public async IAsyncEnumerable<FileSystemItem> SearchAsync(string directoryPath, string query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || string.IsNullOrWhiteSpace(query))
            yield break;

        _logger.LogDebug("Searching for '{Query}' in {Path}", query, directoryPath);

        // Limit recursion depth to prevent searching the entire disk
        const int maxDepth = 5;

        var stack = new Stack<(string Path, int Depth)>();
        stack.Push((directoryPath, 0));

        bool isExtensionSearch = query.StartsWith('.') && query.Length > 1;

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (currentDir, depth) = stack.Pop();
            List<FileSystemItem> items;
            try
            {
                var dirItems = await _fileSystemService.GetItemsAsync(currentDir, cancellationToken);
                items = dirItems.ToList();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogDebug(ex, "Skipping inaccessible directory during search: {Path}", currentDir);
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping directory during search: {Path}", currentDir);
                continue;
            }

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (item.Type == FileSystemItemType.Directory && depth < maxDepth)
                {
                    // Exclude system directories
                    var name = item.Name.ToLowerInvariant();
                    if (name != "$recycle.bin" && name != "system volume information"
                        && !name.StartsWith("$") && name != "windows" && name != "program files"
                        && name != "program files (x86)" && name != "programdata")
                    {
                        stack.Push((item.Path, depth + 1));
                    }
                }

                bool isMatch = false;
                if (isExtensionSearch)
                {
                    isMatch = string.Equals(item.Extension, query, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    isMatch = item.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
                }

                if (isMatch)
                {
                    yield return item;
                }
            }
        }
    }
}
