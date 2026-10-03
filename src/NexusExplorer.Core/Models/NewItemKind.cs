namespace NexusExplorer.Core.Models;

/// <summary>
/// How a <see cref="NewItemDefinition"/> is materialized on disk. This mirrors the semantics of
/// the Windows "ShellNew" registry mechanism, but is OS-neutral so the Core layer stays free of
/// platform code. The actual creation strategy is chosen by the file-operation layer based on
/// this value; the model only <b>describes</b> the option.
/// </summary>
public enum NewItemKind
{
    /// <summary>A directory (the built-in "New Folder" option).</summary>
    Folder,

    /// <summary>
    /// An empty file with a known extension. Corresponds to the ShellNew <c>NullFile</c> value and
    /// is also the safe fallback for any association that only declares an extension.
    /// </summary>
    EmptyFile,

    /// <summary>
    /// A copy of a template file (<see cref="NewItemDefinition.TemplatePath"/>). Corresponds to the
    /// ShellNew <c>FileName</c> value, where Windows ships a prototype document (e.g. a minimal
    /// .docx) under the user's Templates folder.
    /// </summary>
    TemplateFile,

    /// <summary>
    /// A file created from inline byte data declared by the association. Corresponds to the
    /// ShellNew <c>Data</c> value.
    /// </summary>
    DataFile,
}
