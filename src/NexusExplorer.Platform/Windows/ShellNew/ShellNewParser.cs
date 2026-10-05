using NexusExplorer.Core.Models;

namespace NexusExplorer.Platform.Windows.ShellNew;

/// <summary>
/// Pure, registry-agnostic discovery of Windows "ShellNew" associations. Given an
/// <see cref="IRegistryReader"/>, it enumerates file-type extensions, finds the ones that declare a
/// <c>ShellNew</c> entry, interprets which creation mechanism each one uses, and resolves a friendly
/// type name. It never touches the real registry or file system directly, which is what makes the
/// whole discovery path unit-testable with an in-memory reader.
/// </summary>
/// <remarks>
/// Windows exposes "create a new X here" entries under
/// <c>HKCR\.ext\ShellNew</c> or <c>HKCR\.ext\&lt;ProgId&gt;\ShellNew</c>. The ShellNew key carries
/// one of a few values that dictate how the file is produced, which we map to <see cref="NewItemKind"/>:
/// <list type="bullet">
///   <item><c>NullFile</c> → <see cref="NewItemKind.EmptyFile"/> (create an empty file).</item>
///   <item><c>FileName</c> → <see cref="NewItemKind.TemplateFile"/> (copy a template from the user's Templates folder).</item>
///   <item><c>Data</c> → <see cref="NewItemKind.DataFile"/> (write inline bytes).</item>
///   <item><c>Command</c> → intentionally ignored (running an arbitrary registry-declared command is a security risk; see FASE 12).</item>
/// </list>
/// </remarks>
public sealed class ShellNewParser
{
    private readonly IRegistryReader _registry;
    private readonly Func<string, string?>? _templateResolver;

    /// <param name="registry">Read-only registry view (real or fake).</param>
    /// <param name="templateResolver">
    /// Resolves a ShellNew <c>FileName</c> value to an absolute, existing template path, or null if
    /// the template cannot be found. Injected so file-system probing stays out of the parser and can
    /// be faked in tests. When null, template entries fall back to creating an empty file.
    /// </param>
    public ShellNewParser(IRegistryReader registry, Func<string, string?>? templateResolver = null)
    {
        _registry = registry;
        _templateResolver = templateResolver;
    }

    /// <summary>
    /// Discovers every valid ShellNew association. Corrupt or unsupported entries are skipped
    /// (never throwing), so the result always reflects whatever could be interpreted safely.
    /// Results are de-duplicated by extension and sorted by display name.
    /// </summary>
    public IReadOnlyList<NewItemDefinition> Discover()
    {
        var byExtension = new Dictionary<string, NewItemDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var ext in _registry.GetExtensionKeys())
        {
            if (!IsValidExtension(ext))
                continue;

            var definition = TryBuildDefinition(ext);
            if (definition is not null && !byExtension.ContainsKey(ext))
                byExtension[ext] = definition;
        }

        return byExtension.Values
            .OrderBy(d => d.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private NewItemDefinition? TryBuildDefinition(string ext)
    {
        try
        {
            var shellNewKey = FindShellNewKey(ext);
            if (shellNewKey is null)
                return null;

            var kind = ResolveKind(shellNewKey, out var templatePath, out var data);
            if (kind is null)
                return null; // Command-based or empty ShellNew — not creatable safely.

            var displayName = ResolveDisplayName(ext);
            if (string.IsNullOrWhiteSpace(displayName))
                return null;

            return new NewItemDefinition
            {
                DisplayName = displayName!,
                Kind = kind.Value,
                Extension = ext,
                DefaultBaseName = $"New {displayName}",
                TemplatePath = templatePath,
                Data = data,
                Source = NewItemSource.ShellAssociation,
            };
        }
        catch
        {
            // A single malformed association must not break the rest of the menu (FASE 12).
            return null;
        }
    }

    /// <summary>
    /// Finds the ShellNew key for an extension: either directly under the extension key, or under
    /// the extension's default ProgId. Returns the full HKCR-relative key path, or null.
    /// </summary>
    private string? FindShellNewKey(string ext)
    {
        var direct = $"{ext}\\ShellNew";
        if (_registry.KeyExists(direct))
            return direct;

        var progId = _registry.GetDefaultValue(ext);
        if (!string.IsNullOrWhiteSpace(progId))
        {
            var viaProgId = $"{ext}\\{progId}\\ShellNew";
            if (_registry.KeyExists(viaProgId))
                return viaProgId;
        }

        return null;
    }

    /// <summary>
    /// Interprets the ShellNew value set into a creation kind, following the shell's own priority
    /// order. Returns null when the entry only carries a Command (ignored for safety) or is empty.
    /// </summary>
    private NewItemKind? ResolveKind(string shellNewKey, out string? templatePath, out byte[]? data)
    {
        templatePath = null;
        data = null;

        var valueNames = _registry.GetValueNames(shellNewKey);

        // FileName → template copy, but only if the template actually resolves to a real file.
        if (ContainsName(valueNames, "FileName"))
        {
            var fileName = _registry.GetStringValue(shellNewKey, "FileName");
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var resolved = _templateResolver?.Invoke(fileName!);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    templatePath = resolved;
                    return NewItemKind.TemplateFile;
                }
                // Template declared but missing on disk → fall back to an empty file.
                return NewItemKind.EmptyFile;
            }
        }

        // Data → inline bytes.
        if (ContainsName(valueNames, "Data"))
        {
            var bytes = _registry.GetBinaryValue(shellNewKey, "Data");
            if (bytes is { Length: > 0 })
            {
                data = bytes;
                return NewItemKind.DataFile;
            }
        }

        // NullFile → empty file. Also the sensible default when a ShellNew key exists with no
        // usable payload (many associations declare just NullFile="").
        if (ContainsName(valueNames, "NullFile"))
            return NewItemKind.EmptyFile;

        // Command only (or unknown) → do not run arbitrary registry commands.
        if (ContainsName(valueNames, "Command"))
            return null;

        // A bare ShellNew key with no values still means "offer this type"; create empty.
        return NewItemKind.EmptyFile;
    }

    /// <summary>
    /// Resolves a human-friendly type name for the extension, preferring the ProgId's
    /// FriendlyTypeName, then its default value, then the raw extension.
    /// </summary>
    private string? ResolveDisplayName(string ext)
    {
        var progId = _registry.GetDefaultValue(ext);
        if (!string.IsNullOrWhiteSpace(progId))
        {
            var friendly = _registry.GetStringValue(progId!, "FriendlyTypeName");
            if (!string.IsNullOrWhiteSpace(friendly) && !friendly!.StartsWith('@'))
                return friendly;

            var progIdDefault = _registry.GetDefaultValue(progId!);
            if (!string.IsNullOrWhiteSpace(progIdDefault))
                return progIdDefault;
        }

        // Fall back to "<EXT> File" (e.g. ".log" → "LOG File"), matching Explorer's own behavior.
        var bare = ext.TrimStart('.').ToUpperInvariant();
        return string.IsNullOrEmpty(bare) ? null : $"{bare} File";
    }

    private static bool ContainsName(IReadOnlyList<string> names, string target)
    {
        for (var i = 0; i < names.Count; i++)
            if (string.Equals(names[i], target, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>
    /// Validates that a key name is a plausible single file extension (".ext"), rejecting junk,
    /// compound names, and anything with path/invalid characters.
    /// </summary>
    internal static bool IsValidExtension(string ext)
    {
        if (string.IsNullOrWhiteSpace(ext) || ext.Length < 2 || ext[0] != '.')
            return false;

        for (var i = 1; i < ext.Length; i++)
        {
            var c = ext[i];
            if (c == '.' || c == '\\' || c == '/' || char.IsWhiteSpace(c))
                return false;
            if (Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0)
                return false;
        }

        return true;
    }
}
