using NexusExplorer.Core.Models;

namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Service for managing the file operation clipboard (copy/cut/paste paths).
/// Supports accumulative mode: subsequent copies add to the clipboard instead of replacing.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Gets the current clipboard operation, or null if empty.
    /// </summary>
    ClipboardOperation? Current { get; }

    /// <summary>
    /// Whether there are items ready to paste.
    /// </summary>
    bool HasContent { get; }

    /// <summary>
    /// The number of items currently in the clipboard.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Adds paths to the clipboard in Copy mode (accumulative).
    /// If the clipboard was in Cut mode, it switches to Copy.
    /// </summary>
    void SetCopy(IReadOnlyList<string> paths);

    /// <summary>
    /// Sets the clipboard to Cut mode with the given paths (replaces existing — cut is not accumulative).
    /// </summary>
    void SetCut(IReadOnlyList<string> paths);

    /// <summary>
    /// Clears the clipboard.
    /// </summary>
    void Clear();

    /// <summary>
    /// Fired when clipboard content changes.
    /// </summary>
    event EventHandler? ClipboardChanged;
}
