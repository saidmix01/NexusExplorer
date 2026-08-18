using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

public interface IIconProvider
{
    object GetFolderIcon(bool isOpen);
    object GetFileIcon(string extension);
    object GetFileIconForItem(FileSystemItem item);
    object GetAppIcon(string iconName);
}
