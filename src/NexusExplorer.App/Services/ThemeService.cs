using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.Services;

/// <summary>
/// Manages the application theme. Swaps resource dictionaries at runtime to provide
/// real-time theme switching without recreating the visual tree.
///
/// Four themes are supported (see <see cref="ThemeMode"/>):
///   RefinedMinimalism, ModernPastel, AdvancedHierarchy, ContextualDark.
/// Legacy persisted values (Light/Dark/System) are normalized to the closest new theme.
/// </summary>
public sealed class ThemeService
{
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();

    private ResourceDictionary? _currentThemeDictionary;
    private ThemeMode _currentMode = ThemeMode.RefinedMinimalism;

    /// <summary>
    /// The currently active (normalized) theme mode.
    /// </summary>
    public ThemeMode CurrentMode => _currentMode;

    /// <summary>
    /// Whether the current effective theme is a dark theme.
    /// </summary>
    public bool IsDark => Normalize(_currentMode) == ThemeMode.ContextualDark;

    /// <summary>
    /// Applies the given theme. Swaps resource dictionaries on the Application
    /// and adjusts the RequestedThemeVariant so FluentTheme built-in controls also adapt.
    /// </summary>
    public void ApplyTheme(ThemeMode mode)
    {
        var theme = Normalize(mode);
        _currentMode = theme;

        var app = Application.Current;
        if (app is null) return;

        // Remove the old Nexus theme dictionary if present
        if (_currentThemeDictionary is not null)
        {
            app.Resources.MergedDictionaries.Remove(_currentThemeDictionary);
        }

        // Load the appropriate resource dictionary
        var uri = new Uri(GetThemeUri(theme));

        _currentThemeDictionary = new ResourceDictionary();
        var loaded = (ResourceDictionary)AvaloniaXamlLoader.Load(uri);
        // Copy all resources to our tracked dictionary
        foreach (var kvp in loaded)
        {
            _currentThemeDictionary.Add(kvp.Key, kvp.Value);
        }

        app.Resources.MergedDictionaries.Add(_currentThemeDictionary);

        // Also set the Avalonia theme variant so FluentTheme built-in controls adjust
        app.RequestedThemeVariant = theme == ThemeMode.ContextualDark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
    }

    /// <summary>
    /// Maps a (normalized) theme to its packaged resource-dictionary URI.
    /// </summary>
    private static string GetThemeUri(ThemeMode theme) => theme switch
    {
        ThemeMode.ModernPastel => "avares://NexusExplorer.App/Themes/ModernPastelTheme.axaml",
        ThemeMode.AdvancedHierarchy => "avares://NexusExplorer.App/Themes/AdvancedHierarchyTheme.axaml",
        ThemeMode.ContextualDark => "avares://NexusExplorer.App/Themes/ContextualDarkTheme.axaml",
        _ => "avares://NexusExplorer.App/Themes/RefinedMinimalismTheme.axaml"
    };

    /// <summary>
    /// Normalizes any <see cref="ThemeMode"/> (including legacy Light/Dark/System values)
    /// to one of the four current themes. System resolves to light/dark via OS preference.
    /// </summary>
    private static ThemeMode Normalize(ThemeMode mode) => mode switch
    {
        ThemeMode.RefinedMinimalism or
        ThemeMode.ModernPastel or
        ThemeMode.AdvancedHierarchy or
        ThemeMode.ContextualDark => mode,

        // Legacy mappings
        ThemeMode.Light => ThemeMode.RefinedMinimalism,
        ThemeMode.Dark => ThemeMode.ContextualDark,
        ThemeMode.System => IsOsDark() ? ThemeMode.ContextualDark : ThemeMode.RefinedMinimalism,
        _ => ThemeMode.RefinedMinimalism
    };

    /// <summary>
    /// Detects whether the OS is currently in dark mode.
    /// </summary>
    private static bool IsOsDark()
    {
        var app = Application.Current;
        if (app is not null)
        {
            try
            {
                var platformSettings = app.PlatformSettings;
                if (platformSettings is not null)
                {
                    var colorValues = platformSettings.GetColorValues();
                    // PlatformColorValues.ThemeVariant is a PlatformThemeVariant enum (Dark=1)
                    if ((int)colorValues.ThemeVariant == 1)
                        return true;
                }
            }
            catch
            {
                // Fallback if platform settings not available
            }
        }
        return false;
    }
}
