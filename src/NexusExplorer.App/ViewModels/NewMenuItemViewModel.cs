using CommunityToolkit.Mvvm.ComponentModel;
using FluentIcons.Common;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// UI wrapper around a <see cref="NewItemDefinition"/> for the context menu's "New" submenu. It
/// pairs the platform-provided definition (what to create) with a UI-resolved <see cref="Icon"/>
/// (an Avalonia image) so the menu can show each type's native shell icon. Keeping the icon here —
/// rather than on the Core model — preserves the Core layer's freedom from UI/platform imaging
/// types. The <see cref="Definition"/> is the parameter passed to the create command.
/// </summary>
public partial class NewMenuItemViewModel : ObservableObject
{
    public NewMenuItemViewModel(NewItemDefinition definition)
    {
        Definition = definition;
    }

    /// <summary>The underlying description of the "New" option (passed to the create command).</summary>
    public NewItemDefinition Definition { get; }

    /// <summary>Label shown in the menu.</summary>
    public string DisplayName => Definition.DisplayName;

    /// <summary>True for the built-in "Folder" entry, which is created as a directory.</summary>
    public bool IsFolder => Definition.Kind == NewItemKind.Folder;

    /// <summary>
    /// Generic Fluent glyph shown when no native icon is available: a folder for the "Folder" entry,
    /// a document for everything else. This keeps built-ins (which have no file extension to resolve
    /// a shell icon from) looking correct.
    /// </summary>
    public Symbol FallbackSymbol => IsFolder ? Symbol.Folder : Symbol.Document;

    /// <summary>
    /// Resolved native icon for the type, or null while unresolved / unavailable. When null the view
    /// shows <see cref="FallbackSymbol"/> instead.
    /// </summary>
    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? _icon;
}
