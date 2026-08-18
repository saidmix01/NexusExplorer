using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

public interface IFileIconService
{
    object GetIcon(FileSystemItem item);
}