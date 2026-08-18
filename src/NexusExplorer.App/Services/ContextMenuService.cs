using System.Collections.Generic;
using Avalonia.Controls;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.Services;

public interface IContextMenuService
{
    void ShowEmptyAreaContextMenu(Control target, ExplorerViewMode currentViewMode);
    void ShowFileItemContextMenu(Control target, FileSystemItem item);
}

public class ContextMenuService : IContextMenuService
{
    // A placeholder for the service, the actual menu might be constructed in UI or here.
    // If it's UI based, we might not need to build it in C#, but just show it.
    // Avalonia provides ContextMenu property on controls, so maybe this service is for dynamic menus.
    
    public void ShowEmptyAreaContextMenu(Control target, ExplorerViewMode currentViewMode)
    {
        // For now, we will use XAML defined ContextMenus.
    }

    public void ShowFileItemContextMenu(Control target, FileSystemItem item)
    {
        // For now, we will use XAML defined ContextMenus.
    }
}