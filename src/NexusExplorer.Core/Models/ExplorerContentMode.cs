namespace NexusExplorer.Core.Models;

/// <summary>
/// Which content representation the explorer shows for the active tab:
/// the normal file listing, or the logical Project Explorer view.
/// Independent of <see cref="ExplorerViewMode"/> (icons/details) and <see cref="LayoutMode"/>.
/// </summary>
public enum ExplorerContentMode
{
    /// <summary>The standard file explorer (icons/details/list).</summary>
    Files = 0,

    /// <summary>The Project Explorer logical view for detected software projects.</summary>
    Project
}
