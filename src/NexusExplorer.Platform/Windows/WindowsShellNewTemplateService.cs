using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;
using NexusExplorer.Platform.Windows.ShellNew;

namespace NexusExplorer.Platform.Windows;

/// <summary>
/// Windows implementation of <see cref="IShellNewTemplateService"/>. Discovers the entries the
/// shell registers for its "New" menu by reading <c>ShellNew</c> associations from the registry
/// (via <see cref="ShellNewParser"/>), then prepends Nexus's always-available built-ins (Folder and
/// Text Document). This is why Office/LibreOffice/etc. appear only when installed and disappear when
/// uninstalled — nothing is hard-coded.
/// </summary>
/// <remarks>
/// Discovery runs off the UI thread and the result is cached (FASE 8); the cache is dropped on
/// <see cref="InvalidateCache"/>. All failures are contained: discovery itself never throws, and if
/// the whole registry walk fails we still return the built-ins so the menu keeps working.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsShellNewTemplateService : IShellNewTemplateService
{
    private readonly IRegistryReader _registry;
    private readonly ILogger<WindowsShellNewTemplateService> _logger;
    private readonly object _gate = new();

    private IReadOnlyList<NewItemDefinition>? _cache;

    public WindowsShellNewTemplateService(ILogger<WindowsShellNewTemplateService>? logger = null)
        : this(new WindowsRegistryReader(), logger)
    {
    }

    // Test seam: inject a fake registry reader.
    internal WindowsShellNewTemplateService(
        IRegistryReader registry,
        ILogger<WindowsShellNewTemplateService>? logger = null)
    {
        _registry = registry;
        _logger = logger ?? NullLogger<WindowsShellNewTemplateService>.Instance;
    }

    /// <summary>Always-present options, independent of installed apps.</summary>
    private static readonly NewItemDefinition[] BuiltIns =
    [
        new NewItemDefinition
        {
            DisplayName = "Folder",
            Kind = NewItemKind.Folder,
            DefaultBaseName = "New Folder",
            Source = NewItemSource.BuiltIn,
        },
        new NewItemDefinition
        {
            DisplayName = "Text Document",
            Kind = NewItemKind.EmptyFile,
            Extension = ".txt",
            DefaultBaseName = "New Text Document",
            Source = NewItemSource.BuiltIn,
        },
    ];

    public Task<IReadOnlyList<NewItemDefinition>> GetNewItemsAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_cache is not null)
                return Task.FromResult(_cache);
        }

        return Task.Run(() =>
        {
            var result = BuildItems();
            lock (_gate)
            {
                _cache = result;
            }
            return result;
        }, cancellationToken);
    }

    public void InvalidateCache()
    {
        lock (_gate)
        {
            _cache = null;
        }
    }

    private IReadOnlyList<NewItemDefinition> BuildItems()
    {
        var items = new List<NewItemDefinition>(BuiltIns);

        try
        {
            var parser = new ShellNewParser(_registry, ResolveTemplatePath);
            var discovered = parser.Discover();

            // Skip anything that would duplicate a built-in extension (e.g. .txt).
            var builtInExtensions = BuiltIns
                .Select(b => b.Extension)
                .Where(e => !string.IsNullOrEmpty(e))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var item in discovered)
            {
                if (builtInExtensions.Contains(item.Extension))
                    continue;

                // Carry the extension as an opaque icon hint; the UI layer resolves the real icon
                // from the shell and falls back to the generic glyph if that fails.
                items.Add(NewItemDefinitionExtensions.WithIconHint(item));
            }

            _logger.LogDebug(
                "ShellNew discovery found {Count} shell associations (plus {BuiltIn} built-ins).",
                discovered.Count, BuiltIns.Length);
        }
        catch (Exception ex)
        {
            // Never let discovery break the menu — fall back to just the built-ins.
            _logger.LogWarning(ex, "ShellNew discovery failed; falling back to built-in New items only.");
        }

        return items;
    }

    /// <summary>
    /// Resolves a ShellNew <c>FileName</c> template to an absolute, existing path. Windows stores
    /// these under the per-user Templates folder; some entries already carry an absolute path.
    /// Returns null if the template cannot be found so the caller falls back to an empty file.
    /// </summary>
    private static string? ResolveTemplatePath(string fileName)
    {
        try
        {
            if (Path.IsPathRooted(fileName) && File.Exists(fileName))
                return fileName;

            var templates = Environment.GetFolderPath(Environment.SpecialFolder.Templates);
            if (!string.IsNullOrEmpty(templates))
            {
                var candidate = Path.Combine(templates, fileName);
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}

file static class NewItemDefinitionExtensions
{
    /// <summary>
    /// Returns a copy of the definition whose <see cref="NewItemDefinition.IconSource"/> carries the
    /// extension string as a lightweight hint for the UI icon resolver.
    /// </summary>
    public static NewItemDefinition WithIconHint(NewItemDefinition def)
        => new()
        {
            DisplayName = def.DisplayName,
            Kind = def.Kind,
            Extension = def.Extension,
            DefaultBaseName = def.DefaultBaseName,
            TemplatePath = def.TemplatePath,
            Data = def.Data,
            Source = def.Source,
            IconSource = string.IsNullOrEmpty(def.Extension) ? null : def.Extension,
        };
}
