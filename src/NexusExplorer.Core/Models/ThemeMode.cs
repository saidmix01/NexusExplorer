namespace NexusExplorer.Core.Models;

/// <summary>
/// Defines the available visual themes for the application.
/// The four primary values map to the UI/UX theme specification. The legacy
/// <see cref="Light"/>/<see cref="Dark"/>/<see cref="System"/> values are retained
/// so previously persisted preferences keep deserializing; they are mapped onto the
/// new themes at load time (see ThemeService).
/// </summary>
public enum ThemeMode
{
    // --- Legacy values (kept for backward-compatible persistence) ---

    /// <summary>Legacy light theme. Mapped to <see cref="RefinedMinimalism"/>.</summary>
    Light = 0,

    /// <summary>Legacy dark theme. Mapped to <see cref="ContextualDark"/>.</summary>
    Dark = 1,

    /// <summary>Legacy "follow OS" value. Mapped to <see cref="RefinedMinimalism"/>.</summary>
    System = 2,

    // --- Spec themes ---

    /// <summary>Theme 1 — Monochrome minimalism: light, low-noise, maximum clarity.</summary>
    RefinedMinimalism = 10,

    /// <summary>Theme 2 — Modern pastel grid: card-based, chromatic categorization.</summary>
    ModernPastel = 11,

    /// <summary>Theme 3 — Advanced hierarchy: productivity, multi-column, monospace.</summary>
    AdvancedHierarchy = 12,

    /// <summary>Theme 4 — Contextual dark: deep inspection, dark surfaces, indigo accent.</summary>
    ContextualDark = 13
}
