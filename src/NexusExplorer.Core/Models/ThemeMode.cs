namespace NexusExplorer.Core.Models;

/// <summary>
/// Defines the available theme modes for the application.
/// </summary>
public enum ThemeMode
{
    /// <summary>Light theme (macOS Mavericks default).</summary>
    Light = 0,

    /// <summary>Dark theme (graphite dark).</summary>
    Dark = 1,

    /// <summary>Follow the operating system preference.</summary>
    System = 2
}
