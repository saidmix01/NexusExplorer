using NexusExplorer.Core.Models;
using NexusExplorer.Platform.Windows.ShellNew;

namespace NexusExplorer.Tests;

/// <summary>
/// Tests the registry-agnostic ShellNew discovery logic with an in-memory fake registry, so the
/// "New" menu's association parsing is verified without touching the real Windows registry (works
/// on any OS / CI). Covers: no associations, one, many, valid/invalid extensions, uninstalled apps
/// (missing ShellNew), corrupt entries, and template-missing fallback.
/// </summary>
public class ShellNewParserTests
{
    /// <summary>Minimal in-memory <see cref="IRegistryReader"/> for deterministic tests.</summary>
    private sealed class FakeRegistry : IRegistryReader
    {
        public List<string> Extensions { get; } = [];
        public Dictionary<string, string?> DefaultValues { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Dictionary<string, string>> StringValues { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Dictionary<string, byte[]>> BinaryValues { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Keys { get; } = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> GetExtensionKeys() => Extensions;

        public string? GetDefaultValue(string keyPath)
            => DefaultValues.TryGetValue(keyPath, out var v) ? v : null;

        public string? GetStringValue(string keyPath, string valueName)
            => StringValues.TryGetValue(keyPath, out var map) && map.TryGetValue(valueName, out var v) ? v : null;

        public byte[]? GetBinaryValue(string keyPath, string valueName)
            => BinaryValues.TryGetValue(keyPath, out var map) && map.TryGetValue(valueName, out var v) ? v : null;

        public bool KeyExists(string keyPath) => Keys.Contains(keyPath);

        public IReadOnlyList<string> GetValueNames(string keyPath)
        {
            var names = new List<string>();
            if (StringValues.TryGetValue(keyPath, out var s)) names.AddRange(s.Keys);
            if (BinaryValues.TryGetValue(keyPath, out var b)) names.AddRange(b.Keys);
            return names;
        }

        public IReadOnlyList<string> GetSubKeyNames(string keyPath) => [];

        // --- Builder helpers ---

        public FakeRegistry AddExtension(string ext, string? progId = null)
        {
            Extensions.Add(ext);
            if (progId is not null) DefaultValues[ext] = progId;
            return this;
        }

        public FakeRegistry AddKey(string keyPath)
        {
            Keys.Add(keyPath);
            return this;
        }

        public FakeRegistry SetString(string keyPath, string name, string value)
        {
            if (!StringValues.TryGetValue(keyPath, out var map))
                StringValues[keyPath] = map = new(StringComparer.OrdinalIgnoreCase);
            map[name] = value;
            Keys.Add(keyPath);
            return this;
        }

        public FakeRegistry SetBinary(string keyPath, string name, byte[] value)
        {
            if (!BinaryValues.TryGetValue(keyPath, out var map))
                BinaryValues[keyPath] = map = new(StringComparer.OrdinalIgnoreCase);
            map[name] = value;
            Keys.Add(keyPath);
            return this;
        }

        public FakeRegistry SetDefault(string keyPath, string value)
        {
            DefaultValues[keyPath] = value;
            return this;
        }
    }

    [Fact]
    public void Discover_NoAssociations_ReturnsEmpty()
    {
        var reg = new FakeRegistry();
        var result = new ShellNewParser(reg).Discover();
        Assert.Empty(result);
    }

    [Fact]
    public void Discover_SingleNullFileAssociation_ReturnsEmptyFileEntry()
    {
        var reg = new FakeRegistry()
            .AddExtension(".txt", "txtfile")
            .AddKey(".txt\\ShellNew")
            .SetString(".txt\\ShellNew", "NullFile", "")
            .SetDefault("txtfile", "Text Document");

        var result = new ShellNewParser(reg).Discover();

        var item = Assert.Single(result);
        Assert.Equal("Text Document", item.DisplayName);
        Assert.Equal(".txt", item.Extension);
        Assert.Equal(NewItemKind.EmptyFile, item.Kind);
        Assert.Equal("New Text Document", item.DefaultBaseName);
    }

    [Fact]
    public void Discover_MultipleAssociations_ReturnsAllSortedByName()
    {
        var reg = new FakeRegistry()
            .AddExtension(".txt", "txtfile")
            .AddKey(".txt\\ShellNew").SetString(".txt\\ShellNew", "NullFile", "")
            .SetDefault("txtfile", "Text Document");
        reg.AddExtension(".rtf", "rtffile")
            .AddKey(".rtf\\ShellNew").SetString(".rtf\\ShellNew", "NullFile", "")
            .SetDefault("rtffile", "Rich Text Document");

        var result = new ShellNewParser(reg).Discover();

        Assert.Equal(2, result.Count);
        // Sorted alphabetically by display name: "Rich Text Document" before "Text Document".
        Assert.Equal("Rich Text Document", result[0].DisplayName);
        Assert.Equal("Text Document", result[1].DisplayName);
    }

    [Fact]
    public void Discover_ShellNewUnderProgId_IsFound()
    {
        var reg = new FakeRegistry()
            .AddExtension(".docx", "Word.Document.12")
            .AddKey(".docx\\Word.Document.12\\ShellNew")
            .SetString(".docx\\Word.Document.12\\ShellNew", "FileName", "winword.docx")
            .SetDefault("Word.Document.12", "Microsoft Word Document");

        // Template resolver says the template is missing → should fall back to EmptyFile, still discovered.
        var result = new ShellNewParser(reg, _ => null).Discover();

        var item = Assert.Single(result);
        Assert.Equal("Microsoft Word Document", item.DisplayName);
        Assert.Equal(NewItemKind.EmptyFile, item.Kind);
        Assert.Equal(".docx", item.Extension);
    }

    [Fact]
    public void Discover_FileNameWithResolvableTemplate_ReturnsTemplateFile()
    {
        var reg = new FakeRegistry()
            .AddExtension(".docx", "Word.Document.12")
            .AddKey(".docx\\Word.Document.12\\ShellNew")
            .SetString(".docx\\Word.Document.12\\ShellNew", "FileName", "winword.docx")
            .SetDefault("Word.Document.12", "Microsoft Word Document");

        var result = new ShellNewParser(reg, _ => @"C:\Templates\winword.docx").Discover();

        var item = Assert.Single(result);
        Assert.Equal(NewItemKind.TemplateFile, item.Kind);
        Assert.Equal(@"C:\Templates\winword.docx", item.TemplatePath);
    }

    [Fact]
    public void Discover_DataAssociation_ReturnsDataFile()
    {
        var reg = new FakeRegistry()
            .AddExtension(".ico", "icofile")
            .AddKey(".ico\\ShellNew")
            .SetBinary(".ico\\ShellNew", "Data", [1, 2, 3, 4])
            .SetDefault("icofile", "Icon File");

        var result = new ShellNewParser(reg).Discover();

        var item = Assert.Single(result);
        Assert.Equal(NewItemKind.DataFile, item.Kind);
        Assert.Equal([1, 2, 3, 4], item.Data);
    }

    [Fact]
    public void Discover_CommandOnlyAssociation_IsSkippedForSafety()
    {
        var reg = new FakeRegistry()
            .AddExtension(".xyz", "xyzfile")
            .AddKey(".xyz\\ShellNew")
            .SetString(".xyz\\ShellNew", "Command", "evil.exe %1")
            .SetDefault("xyzfile", "XYZ Document");

        var result = new ShellNewParser(reg).Discover();

        // Command-based ShellNew entries must never be turned into a creatable item.
        Assert.Empty(result);
    }

    [Fact]
    public void Discover_ExtensionWithoutShellNew_IsIgnored()
    {
        // App "uninstalled" scenario: the extension key lingers but no ShellNew entry remains.
        var reg = new FakeRegistry()
            .AddExtension(".old", "oldfile")
            .SetDefault("oldfile", "Old Document");

        var result = new ShellNewParser(reg).Discover();

        Assert.Empty(result);
    }

    [Fact]
    public void Discover_InvalidExtensionKeys_AreIgnored()
    {
        var reg = new FakeRegistry();
        reg.Extensions.Add("notanext");    // no leading dot
        reg.Extensions.Add(".");           // just a dot
        reg.Extensions.Add(".a.b");        // compound
        reg.AddKey("notanext\\ShellNew");

        var result = new ShellNewParser(reg).Discover();

        Assert.Empty(result);
    }

    [Fact]
    public void Discover_NoFriendlyName_FallsBackToExtensionLabel()
    {
        var reg = new FakeRegistry()
            .AddExtension(".log")
            .AddKey(".log\\ShellNew")
            .SetString(".log\\ShellNew", "NullFile", "");

        var result = new ShellNewParser(reg).Discover();

        var item = Assert.Single(result);
        Assert.Equal("LOG File", item.DisplayName);
    }

    [Fact]
    public void Discover_DuplicateExtension_KeepsSingleEntry()
    {
        var reg = new FakeRegistry();
        reg.Extensions.Add(".txt");
        reg.Extensions.Add(".txt"); // duplicate key name
        reg.AddKey(".txt\\ShellNew").SetString(".txt\\ShellNew", "NullFile", "");

        var result = new ShellNewParser(reg).Discover();

        Assert.Single(result);
    }

    [Theory]
    [InlineData(".txt", true)]
    [InlineData(".docx", true)]
    [InlineData("txt", false)]
    [InlineData(".", false)]
    [InlineData("", false)]
    [InlineData(".a.b", false)]
    public void IsValidExtension_Works(string ext, bool expected)
        => Assert.Equal(expected, ShellNewParser.IsValidExtension(ext));
}
