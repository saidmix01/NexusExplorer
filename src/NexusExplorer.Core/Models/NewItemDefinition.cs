namespace NexusExplorer.Core.Models;

/// <summary>
/// Describes a single entry in the context menu's "New" submenu (e.g. "Folder",
/// "Text Document", "Microsoft Word Document"). This is a pure, immutable <b>description</b> of an
/// option — it never touches the file system or the Windows registry. The platform layer produces
/// these (on Windows, by reading the shell's registered <c>ShellNew</c> entries), and the
/// file-operation layer consumes <see cref="Kind"/> to decide how to create the item.
/// </summary>
/// <remarks>
/// Keeping this model free of execution logic preserves the layering
/// (UI → ViewModel → abstraction → platform implementation → registry/shell) and keeps it unit
/// testable without a real registry.
/// </remarks>
public sealed class NewItemDefinition
{
    /// <summary>Human-readable label shown in the menu (e.g. "Microsoft Word Document").</summary>
    public required string DisplayName { get; init; }

    /// <summary>How the item is created. See <see cref="NewItemKind"/>.</summary>
    public required NewItemKind Kind { get; init; }

    /// <summary>
    /// File extension including the leading dot (e.g. ".docx"). Empty for <see cref="NewItemKind.Folder"/>.
    /// </summary>
    public string Extension { get; init; } = string.Empty;

    /// <summary>
    /// Base file name used when creating the item, without extension and before any
    /// de-duplication suffix (e.g. "New Microsoft Word Document"). The file-operation layer
    /// appends <see cref="Extension"/> and, if needed, a " (2)" style suffix.
    /// </summary>
    public required string DefaultBaseName { get; init; }

    /// <summary>
    /// For <see cref="NewItemKind.TemplateFile"/>, the absolute path of the template to copy.
    /// Null for every other kind.
    /// </summary>
    public string? TemplatePath { get; init; }

    /// <summary>
    /// For <see cref="NewItemKind.DataFile"/>, the inline bytes to write. Null for other kinds.
    /// </summary>
    public byte[]? Data { get; init; }

    /// <summary>
    /// Optional platform-provided icon source for the type. On Windows this is typically an
    /// absolute path produced by the shell icon resolver; the UI layer converts it to an image and
    /// falls back to Nexus's generic glyph when null or unavailable. The Core layer treats it as an
    /// opaque hint and never loads it.
    /// </summary>
    public object? IconSource { get; init; }

    /// <summary>
    /// Where this definition came from, for diagnostics and ordering (built-in Nexus options vs.
    /// shell-discovered associations).
    /// </summary>
    public NewItemSource Source { get; init; } = NewItemSource.ShellAssociation;
}

/// <summary>Origin of a <see cref="NewItemDefinition"/>.</summary>
public enum NewItemSource
{
    /// <summary>A fixed option Nexus always offers (Folder, Text Document).</summary>
    BuiltIn,

    /// <summary>Discovered from the OS shell's registered file-type associations.</summary>
    ShellAssociation,
}
