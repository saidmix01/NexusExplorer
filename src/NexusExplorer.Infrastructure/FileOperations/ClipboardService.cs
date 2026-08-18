using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Infrastructure.FileOperations;

/// <summary>
/// In-memory clipboard for file operations (copy/cut paths).
/// Copy is accumulative: each Ctrl+C adds to the existing clipboard.
/// Cut replaces the clipboard entirely (you can't accumulate cuts).
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    private readonly List<string> _paths = [];

    public ClipboardOperation? Current => _paths.Count > 0
        ? new ClipboardOperation { Paths = _paths.ToList().AsReadOnly(), IsCut = _isCut }
        : null;

    public bool HasContent => _paths.Count > 0;
    public int Count => _paths.Count;

    private bool _isCut;

    public event EventHandler? ClipboardChanged;

    /// <summary>
    /// Adds paths to the clipboard (accumulative). Duplicates are ignored.
    /// If the clipboard was in Cut mode, it resets to Copy mode.
    /// </summary>
    public void SetCopy(IReadOnlyList<string> paths)
    {
        _isCut = false;
        foreach (var path in paths)
        {
            if (!_paths.Contains(path, StringComparer.OrdinalIgnoreCase))
                _paths.Add(path);
        }
        ClipboardChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Sets the clipboard to Cut mode (replaces all existing content).
    /// Cut is not accumulative — it's a single operation.
    /// </summary>
    public void SetCut(IReadOnlyList<string> paths)
    {
        _paths.Clear();
        _paths.AddRange(paths);
        _isCut = true;
        ClipboardChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _paths.Clear();
        _isCut = false;
        ClipboardChanged?.Invoke(this, EventArgs.Empty);
    }
}
