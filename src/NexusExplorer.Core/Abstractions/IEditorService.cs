using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Discovers installed code editors and opens folders with them.
/// </summary>
public interface IEditorService
{
    IReadOnlyList<EditorInfo> GetInstalledEditors();

    void Open(EditorInfo editor, string folderPath);
}
