using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.Services;

/// <summary>
/// Manages the application theme (Light / Dark / System).
/// Swaps resource dictionaries at runtime to provide real-time theme switching
/// without recreating the visual tree.
/// </summary>
public sealed class ThemeService
{
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();

    private ResourceDictionary? _currentThemeDictionary;
    private ThemeMode _currentMode = ThemeMode.Light;

    /// <summary>
    /// The currently active theme mode.
    /// </summary>
    public ThemeMode CurrentMode => _currentMode;

    /// <summary>
    /// Whether the current effective theme is dark.
    /// </summary>
    public bool IsDark => GetEffectiveTheme(_currentMode) == ThemeMode.Dark;

    /// <summary>
    /// Applies the given theme mode. Swaps resource dictionaries on the Application
    /// and adjusts the RequestedThemeVariant so FluentTheme built-in controls also adapt.
    /// </summary>
    public void ApplyTheme(ThemeMode mode)
    {
        _currentMode = mode;
        var effectiveTheme = GetEffectiveTheme(mode);
        var app = Application.Current;
        if (app is null) return;

        // Remove the old Nexus theme dictionary if present
        if (_currentThemeDictionary is not null)
        {
            app.Resources.MergedDictionaries.Remove(_currentThemeDictionary);
        }

        // Load the appropriate resource dictionary
        var uri = effectiveTheme == ThemeMode.Dark
            ? new Uri("avares://NexusExplorer.App/Themes/NexusDarkTheme.axaml")
            : new Uri("avares://NexusExplorer.App/Themes/NexusLightTheme.axaml");

        _currentThemeDictionary = new ResourceDictionary();
        var loaded = (ResourceDictionary)AvaloniaXamlLoader.Load(uri);
        // Copy all resources to our tracked dictionary
        foreach (var kvp in loaded)
        {
            _currentThemeDictionary.Add(kvp.Key, kvp.Value);
        }

        app.Resources.MergedDictionaries.Add(_currentThemeDictionary);

        // Also set the Avalonia theme variant so FluentTheme built-in controls adjust
        app.RequestedThemeVariant = effectiveTheme == ThemeMode.Dark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
    }

    /// <summary>
    /// Resolves <see cref="ThemeMode.System"/> to the actual effective theme.
    /// </summary>
    private static ThemeMode GetEffectiveTheme(ThemeMode mode)
    {
        if (mode == ThemeMode.System)
        {
            // Detect OS dark mode by checking the current app's actual theme variant
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
                            return ThemeMode.Dark;
                    }
                }
                catch
                {
                    // Fallback if platform settings not available
                }
            }
            return ThemeMode.Light;
        }
        return mode;
    }
}
