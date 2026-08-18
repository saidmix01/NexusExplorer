using FluentIcons.Common;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;
using System.Collections.Frozen;
using System.IO;

namespace NexusExplorer.App.Services;

/// <summary>
/// Centralized icon resolver that maps file extensions, known filenames,
/// and app icon names to FluentIcons Symbol enum values.
/// All icon resolution logic lives here — no duplication in views.
/// </summary>
public sealed class FluentIconProvider : IIconProvider
{
    // Frozen dictionaries for O(1) lookup performance
    private static readonly FrozenDictionary<string, Symbol> ExtensionToSymbol = BuildExtensionMap();
    private static readonly FrozenDictionary<string, Symbol> KnownFileNames = BuildKnownFileNameMap();

    public object GetFolderIcon(bool isOpen)
    {
        return isOpen ? Symbol.FolderOpen : Symbol.Folder;
    }

    public object GetFileIcon(string extension)
    {
        var ext = extension.ToLowerInvariant();
        if (ext.Length > 0 && ext[0] != '.')
            ext = "." + ext;

        return ExtensionToSymbol.GetValueOrDefault(ext, Symbol.Document);
    }

    /// <summary>
    /// Resolves icon for a FileSystemItem, considering known filenames first,
    /// then extension, then fallback.
    /// </summary>
    public object GetFileIconForItem(FileSystemItem item)
    {
        if (item.Type == FileSystemItemType.Directory)
            return Symbol.Folder;

        if (item.Type == FileSystemItemType.Drive)
            return Symbol.HardDrive;

        if (item.Type == FileSystemItemType.SymbolicLink)
            return Symbol.Link;

        // Check known filenames first (case-insensitive)
        var fileName = item.Name.ToLowerInvariant();
        if (KnownFileNames.TryGetValue(fileName, out var knownSymbol))
            return knownSymbol;

        // Check filename without leading dot for dotfiles like .gitignore
        if (fileName.StartsWith('.') && KnownFileNames.TryGetValue(fileName, out var dotfileSymbol))
            return dotfileSymbol;

        // Fall back to extension mapping
        var ext = (item.Extension ?? Path.GetExtension(item.Name))?.ToLowerInvariant() ?? string.Empty;
        return ExtensionToSymbol.GetValueOrDefault(ext, Symbol.Document);
    }

    public object GetAppIcon(string iconName)
    {
        return iconName switch
        {
            "Back" => Symbol.ArrowLeft,
            "Forward" => Symbol.ArrowRight,
            "Up" => Symbol.ArrowUp,
            "Refresh" => Symbol.ArrowClockwise,
            "Search" => Symbol.Search,
            "NewTab" => Symbol.Add,
            "CloseTab" => Symbol.Dismiss,
            "Terminal" => Symbol.WindowConsole,
            "Split" => Symbol.PanelRight,
            "Preview" => Symbol.Eye,
            "Menu" => Symbol.Navigation,
            "Settings" => Symbol.Settings,
            "Copy" => Symbol.Copy,
            "Cut" => Symbol.Cut,
            "Paste" => Symbol.ClipboardPaste,
            "Delete" => Symbol.Delete,
            "Rename" => Symbol.Rename,
            "Properties" => Symbol.Info,
            "NewFolder" => Symbol.FolderAdd,
            "NewFile" => Symbol.DocumentAdd,
            "Home" => Symbol.Home,
            "Desktop" => Symbol.Desktop,
            "Documents" => Symbol.Document,
            "Downloads" => Symbol.ArrowDownload,
            "Pictures" => Symbol.Image,
            "Videos" => Symbol.Video,
            "Music" => Symbol.MusicNote2,
            "Drive" => Symbol.HardDrive,
            "Network" => Symbol.Globe,
            "Folder" => Symbol.Folder,
            "FolderOpen" => Symbol.FolderOpen,
            _ => Symbol.Question
        };
    }

    private static FrozenDictionary<string, Symbol> BuildExtensionMap()
    {
        var map = new Dictionary<string, Symbol>(StringComparer.OrdinalIgnoreCase)
        {
            // --- Documents ---
            [".txt"] = Symbol.DocumentText,
            [".rtf"] = Symbol.DocumentText,
            [".log"] = Symbol.DocumentText,
            [".md"] = Symbol.DocumentText,
            [".pdf"] = Symbol.DocumentPdf,
            [".doc"] = Symbol.DocumentText,
            [".docx"] = Symbol.DocumentText,

            // --- Spreadsheets ---
            [".xls"] = Symbol.Table,
            [".xlsx"] = Symbol.Table,
            [".csv"] = Symbol.Table,

            // --- Presentations ---
            [".ppt"] = Symbol.Board,
            [".pptx"] = Symbol.Board,

            // --- Code ---
            [".cs"] = Symbol.Code,
            [".js"] = Symbol.Code,
            [".ts"] = Symbol.Code,
            [".jsx"] = Symbol.Code,
            [".tsx"] = Symbol.Code,
            [".html"] = Symbol.Code,
            [".htm"] = Symbol.Code,
            [".css"] = Symbol.Code,
            [".scss"] = Symbol.Code,
            [".sass"] = Symbol.Code,
            [".less"] = Symbol.Code,
            [".json"] = Symbol.BracesVariable,
            [".xml"] = Symbol.Code,
            [".yaml"] = Symbol.Code,
            [".yml"] = Symbol.Code,
            [".sql"] = Symbol.Database,
            [".py"] = Symbol.Code,
            [".rs"] = Symbol.Code,
            [".java"] = Symbol.Code,
            [".cpp"] = Symbol.Code,
            [".c"] = Symbol.Code,
            [".h"] = Symbol.Code,
            [".hpp"] = Symbol.Code,
            [".php"] = Symbol.Code,
            [".go"] = Symbol.Code,
            [".rb"] = Symbol.Code,
            [".swift"] = Symbol.Code,
            [".kt"] = Symbol.Code,
            [".dart"] = Symbol.Code,
            [".lua"] = Symbol.Code,
            [".r"] = Symbol.Code,
            [".m"] = Symbol.Code,
            [".sh"] = Symbol.WindowConsole,
            [".bat"] = Symbol.WindowConsole,
            [".cmd"] = Symbol.WindowConsole,
            [".ps1"] = Symbol.WindowConsole,
            [".psm1"] = Symbol.WindowConsole,

            // --- Data/Config ---
            [".ini"] = Symbol.Settings,
            [".cfg"] = Symbol.Settings,
            [".conf"] = Symbol.Settings,
            [".toml"] = Symbol.Settings,
            [".env"] = Symbol.Settings,
            [".properties"] = Symbol.Settings,

            // --- Images ---
            [".png"] = Symbol.Image,
            [".jpg"] = Symbol.Image,
            [".jpeg"] = Symbol.Image,
            [".gif"] = Symbol.Image,
            [".bmp"] = Symbol.Image,
            [".webp"] = Symbol.Image,
            [".svg"] = Symbol.Image,
            [".ico"] = Symbol.Image,
            [".tiff"] = Symbol.Image,
            [".tif"] = Symbol.Image,
            [".raw"] = Symbol.Image,
            [".psd"] = Symbol.Image,

            // --- Audio ---
            [".mp3"] = Symbol.MusicNote2,
            [".wav"] = Symbol.MusicNote2,
            [".flac"] = Symbol.MusicNote2,
            [".ogg"] = Symbol.MusicNote2,
            [".m4a"] = Symbol.MusicNote2,
            [".aac"] = Symbol.MusicNote2,
            [".wma"] = Symbol.MusicNote2,
            [".opus"] = Symbol.MusicNote2,

            // --- Video ---
            [".mp4"] = Symbol.Video,
            [".mkv"] = Symbol.Video,
            [".avi"] = Symbol.Video,
            [".mov"] = Symbol.Video,
            [".webm"] = Symbol.Video,
            [".wmv"] = Symbol.Video,
            [".flv"] = Symbol.Video,
            [".m4v"] = Symbol.Video,

            // --- Archives ---
            [".zip"] = Symbol.FolderZip,
            [".rar"] = Symbol.FolderZip,
            [".7z"] = Symbol.FolderZip,
            [".tar"] = Symbol.FolderZip,
            [".gz"] = Symbol.FolderZip,
            [".bz2"] = Symbol.FolderZip,
            [".xz"] = Symbol.FolderZip,
            [".zst"] = Symbol.FolderZip,

            // --- Executables / System ---
            [".exe"] = Symbol.AppGeneric,
            [".msi"] = Symbol.AppGeneric,
            [".dll"] = Symbol.Library,
            [".sys"] = Symbol.HardDrive,
            [".drv"] = Symbol.HardDrive,

            // --- Fonts ---
            [".ttf"] = Symbol.TextFont,
            [".otf"] = Symbol.TextFont,
            [".woff"] = Symbol.TextFont,
            [".woff2"] = Symbol.TextFont,

            // --- Certificates / Security ---
            [".cer"] = Symbol.Certificate,
            [".crt"] = Symbol.Certificate,
            [".pem"] = Symbol.Certificate,
            [".key"] = Symbol.Key,
            [".pfx"] = Symbol.ShieldKeyhole,

            // --- Database ---
            [".db"] = Symbol.Database,
            [".sqlite"] = Symbol.Database,
            [".mdb"] = Symbol.Database,

            // --- Disk images ---
            [".iso"] = Symbol.HardDrive,
            [".img"] = Symbol.HardDrive,
            [".vhd"] = Symbol.HardDrive,
            [".vmdk"] = Symbol.HardDrive,

            // --- Shortcuts / Links ---
            [".lnk"] = Symbol.Link,
            [".url"] = Symbol.Link,
            [".desktop"] = Symbol.Link, // Linux desktop shortcuts
        };

        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static FrozenDictionary<string, Symbol> BuildKnownFileNameMap()
    {
        var map = new Dictionary<string, Symbol>(StringComparer.OrdinalIgnoreCase)
        {
            // Well-known files without extension or with special meaning
            ["readme"] = Symbol.BookOpen,
            ["readme.md"] = Symbol.BookOpen,
            ["readme.txt"] = Symbol.BookOpen,
            ["license"] = Symbol.Certificate,
            ["license.md"] = Symbol.Certificate,
            ["license.txt"] = Symbol.Certificate,
            ["licence"] = Symbol.Certificate,
            ["changelog"] = Symbol.ClipboardTextEdit,
            ["changelog.md"] = Symbol.ClipboardTextEdit,
            ["dockerfile"] = Symbol.Box,
            ["docker-compose.yml"] = Symbol.Box,
            ["docker-compose.yaml"] = Symbol.Box,
            ["makefile"] = Symbol.Wrench,
            ["cmakelists.txt"] = Symbol.Wrench,
            ["rakefile"] = Symbol.Wrench,
            [".gitignore"] = Symbol.BranchFork,
            [".gitattributes"] = Symbol.BranchFork,
            [".gitmodules"] = Symbol.BranchFork,
            [".editorconfig"] = Symbol.Settings,
            [".eslintrc"] = Symbol.Settings,
            [".eslintrc.json"] = Symbol.Settings,
            [".prettierrc"] = Symbol.Settings,
            [".babelrc"] = Symbol.Settings,
            ["package.json"] = Symbol.Box,
            ["package-lock.json"] = Symbol.BoxCheckmark,
            ["yarn.lock"] = Symbol.BoxCheckmark,
            ["pnpm-lock.yaml"] = Symbol.BoxCheckmark,
            ["tsconfig.json"] = Symbol.Code,
            ["jsconfig.json"] = Symbol.Code,
            ["nuget.config"] = Symbol.Settings,
            ["global.json"] = Symbol.Settings,
            ["appsettings.json"] = Symbol.Settings,
            ["appsettings.development.json"] = Symbol.Settings,
            ["web.config"] = Symbol.Settings,
            ["app.config"] = Symbol.Settings,
            [".env"] = Symbol.Key,
            [".env.local"] = Symbol.Key,
            [".env.development"] = Symbol.Key,
            [".env.production"] = Symbol.Key,
            ["procfile"] = Symbol.Cloud,
            [".dockerignore"] = Symbol.Box,
            [".npmrc"] = Symbol.Settings,
            [".nvmrc"] = Symbol.Settings,
            ["cargo.toml"] = Symbol.Box,
            ["cargo.lock"] = Symbol.BoxCheckmark,
            ["go.mod"] = Symbol.Box,
            ["go.sum"] = Symbol.BoxCheckmark,
            ["gemfile"] = Symbol.Box,
            ["gemfile.lock"] = Symbol.BoxCheckmark,
            ["requirements.txt"] = Symbol.Box,
            ["pipfile"] = Symbol.Box,
            ["pipfile.lock"] = Symbol.BoxCheckmark,
        };

        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
