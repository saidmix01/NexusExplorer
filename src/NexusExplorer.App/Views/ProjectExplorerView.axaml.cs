using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using NexusExplorer.App.ViewModels;
using NexusExplorer.Core.Models.Projects;

namespace NexusExplorer.App.Views;

/// <summary>
/// Logical, read-only view of a detected software project. Double-tapping a mapped entry opens
/// the real file/folder through the view model, which reuses the normal explorer navigation.
/// </summary>
public partial class ProjectExplorerView : UserControl
{
    public ProjectExplorerView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void ProjectEntry_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { Tag: ProjectEntry entry })
            return;
        if (DataContext is not ProjectExplorerViewModel vm)
            return;

        if (vm.OpenEntryCommand.CanExecute(entry))
            vm.OpenEntryCommand.Execute(entry);
    }
}
