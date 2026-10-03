using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using FluentIcons.Common;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// Resolves the glyph shown on the unified view/layout dropdown button, reflecting the
/// currently active mode. Bindings: [0] = ViewMode (ExplorerViewMode), [1] = LayoutMode.
/// Priority: Terminal-only and Split layouts win over the explorer content view; otherwise
/// the icon reflects the explorer view (grid icons vs details list).
/// </summary>
public sealed class ActiveViewIconConverter : IMultiValueConverter
{
    public static readonly ActiveViewIconConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var viewMode = values.Count > 0 && values[0] is ExplorerViewMode vm ? vm : ExplorerViewMode.Details;
        var layout = values.Count > 1 && values[1] is LayoutMode lm ? lm : LayoutMode.ExplorerOnly;

        if (layout == LayoutMode.TerminalOnly)
            return Symbol.WindowConsole;
        if (layout == LayoutMode.Split)
            return Symbol.PanelRight;

        // Explorer-only: reflect the content view mode.
        return viewMode == ExplorerViewMode.Details ? Symbol.AppsList : Symbol.Grid;
    }
}
