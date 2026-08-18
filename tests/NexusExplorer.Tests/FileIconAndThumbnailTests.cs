using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using FluentIcons.Common;
using Moq;
using NexusExplorer.App.Services;
using NexusExplorer.App.Services.Thumbnails;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;
using Xunit;

namespace NexusExplorer.Tests;

public class FileIconAndThumbnailTests
{
    private readonly FluentIconProvider _iconProvider;
    private readonly FluentFileIconService _iconService;
    private readonly ThumbnailService _thumbnailService;

    public FileIconAndThumbnailTests()
    {
        _iconProvider = new FluentIconProvider();
        _iconService = new FluentFileIconService(_iconProvider);
        _thumbnailService = new ThumbnailService();
    }

    // ================================================================
    // 1. Folder → Folder icon
    // ================================================================

    [Fact]
    public void Folder_ReturnsFolderIcon()
    {
        var item = new FileSystemItem { Name = "MyFolder", Path = "/MyFolder", Type = FileSystemItemType.Directory };
        var icon = _iconService.GetIcon(item);
        Assert.Equal(Symbol.Folder, icon);
    }

    [Fact]
    public void FolderIcon_OpenAndClosed()
    {
        Assert.Equal(Symbol.Folder, _iconProvider.GetFolderIcon(isOpen: false));
        Assert.Equal(Symbol.FolderOpen, _iconProvider.GetFolderIcon(isOpen: true));
    }

    // ================================================================
    // 2. TXT → Text icon
    // ================================================================

    [Fact]
    public void TxtFile_ReturnsDocumentTextIcon()
    {
        var item = new FileSystemItem { Name = "notes.txt", Path = "/notes.txt", Type = FileSystemItemType.File, Extension = ".txt" };
        var icon = _iconService.GetIcon(item);
        Assert.Equal(Symbol.DocumentText, icon);
    }

    // ================================================================
    // 3. PDF → PDF icon
    // ================================================================

    [Fact]
    public void PdfFile_ReturnsDocumentPdfIcon()
    {
        var item = new FileSystemItem { Name = "report.pdf", Path = "/report.pdf", Type = FileSystemItemType.File, Extension = ".pdf" };
        var icon = _iconService.GetIcon(item);
        Assert.Equal(Symbol.DocumentPdf, icon);
    }

    // ================================================================
    // 4. PNG → image thumbnail/icon
    // ================================================================

    [Fact]
    public void PngFile_ReturnsImageIcon()
    {
        var item = new FileSystemItem { Name = "photo.png", Path = "/photo.png", Type = FileSystemItemType.File, Extension = ".png" };
        var icon = _iconService.GetIcon(item);
        Assert.Equal(Symbol.Image, icon);
    }

    [Fact]
    public void PngFile_IsEligibleForThumbnail()
    {
        var item = new FileSystemItem { Name = "photo.png", Path = "/photo.png", Type = FileSystemItemType.File, Extension = ".png" };
        Assert.True(_thumbnailService.CanGenerateThumbnail(item));
    }

    // ================================================================
    // 5. JPG → image thumbnail/icon
    // ================================================================

    [Fact]
    public void JpgFile_ReturnsImageIcon()
    {
        var item = new FileSystemItem { Name = "vacation.jpg", Path = "/vacation.jpg", Type = FileSystemItemType.File, Extension = ".jpg" };
        var icon = _iconService.GetIcon(item);
        Assert.Equal(Symbol.Image, icon);
    }

    [Fact]
    public void JpgFile_IsEligibleForThumbnail()
    {
        var item = new FileSystemItem { Name = "vacation.jpg", Path = "/vacation.jpg", Type = FileSystemItemType.File, Extension = ".jpg" };
        Assert.True(_thumbnailService.CanGenerateThumbnail(item));
    }

    [Fact]
    public void JpegFile_IsEligibleForThumbnail()
    {
        var item = new FileSystemItem { Name = "photo.jpeg", Path = "/photo.jpeg", Type = FileSystemItemType.File, Extension = ".jpeg" };
        Assert.True(_thumbnailService.CanGenerateThumbnail(item));
    }

    // ================================================================
    // 6. MP3 → audio icon
    // ================================================================

    [Fact]
    public void Mp3File_ReturnsMusicIcon()
    {
        var item = new FileSystemItem { Name = "song.mp3", Path = "/song.mp3", Type = FileSystemItemType.File, Extension = ".mp3" };
        var icon = _iconService.GetIcon(item);
        Assert.Equal(Symbol.MusicNote2, icon);
    }

    [Theory]
    [InlineData(".wav")]
    [InlineData(".flac")]
    [InlineData(".ogg")]
    [InlineData(".m4a")]
    [InlineData(".aac")]
    public void AudioFiles_ReturnMusicIcon(string ext)
    {
        var item = new FileSystemItem { Name = $"audio{ext}", Path = $"/audio{ext}", Type = FileSystemItemType.File, Extension = ext };
        Assert.Equal(Symbol.MusicNote2, _iconService.GetIcon(item));
    }

    // ================================================================
    // 7. MP4 → video icon
    // ================================================================

    [Fact]
    public void Mp4File_ReturnsVideoIcon()
    {
        var item = new FileSystemItem { Name = "clip.mp4", Path = "/clip.mp4", Type = FileSystemItemType.File, Extension = ".mp4" };
        var icon = _iconService.GetIcon(item);
        Assert.Equal(Symbol.Video, icon);
    }

    [Theory]
    [InlineData(".mkv")]
    [InlineData(".avi")]
    [InlineData(".mov")]
    [InlineData(".webm")]
    public void VideoFiles_ReturnVideoIcon(string ext)
    {
        var item = new FileSystemItem { Name = $"video{ext}", Path = $"/video{ext}", Type = FileSystemItemType.File, Extension = ext };
        Assert.Equal(Symbol.Video, _iconService.GetIcon(item));
    }

    // ================================================================
    // 8. ZIP → archive icon
    // ================================================================

    [Fact]
    public void ZipFile_ReturnsFolderZipIcon()
    {
        var item = new FileSystemItem { Name = "backup.zip", Path = "/backup.zip", Type = FileSystemItemType.File, Extension = ".zip" };
        var icon = _iconService.GetIcon(item);
        Assert.Equal(Symbol.FolderZip, icon);
    }

    [Theory]
    [InlineData(".rar")]
    [InlineData(".7z")]
    [InlineData(".tar")]
    [InlineData(".gz")]
    public void ArchiveFiles_ReturnFolderZipIcon(string ext)
    {
        var item = new FileSystemItem { Name = $"archive{ext}", Path = $"/archive{ext}", Type = FileSystemItemType.File, Extension = ext };
        Assert.Equal(Symbol.FolderZip, _iconService.GetIcon(item));
    }

    // ================================================================
    // 9. EXE → executable icon
    // ================================================================

    [Fact]
    public void ExeFile_ReturnsAppGenericIcon()
    {
        var item = new FileSystemItem { Name = "setup.exe", Path = "/setup.exe", Type = FileSystemItemType.File, Extension = ".exe" };
        var icon = _iconService.GetIcon(item);
        Assert.Equal(Symbol.AppGeneric, icon);
    }

    [Fact]
    public void MsiFile_ReturnsAppGenericIcon()
    {
        var item = new FileSystemItem { Name = "installer.msi", Path = "/installer.msi", Type = FileSystemItemType.File, Extension = ".msi" };
        Assert.Equal(Symbol.AppGeneric, _iconService.GetIcon(item));
    }

    [Fact]
    public void DllFile_ReturnsLibraryIcon()
    {
        var item = new FileSystemItem { Name = "core.dll", Path = "/core.dll", Type = FileSystemItemType.File, Extension = ".dll" };
        Assert.Equal(Symbol.Library, _iconService.GetIcon(item));
    }

    // ================================================================
    // 10. CS → code icon
    // ================================================================

    [Fact]
    public void CsFile_ReturnsCodeIcon()
    {
        var item = new FileSystemItem { Name = "Program.cs", Path = "/Program.cs", Type = FileSystemItemType.File, Extension = ".cs" };
        var icon = _iconService.GetIcon(item);
        Assert.Equal(Symbol.Code, icon);
    }

    [Theory]
    [InlineData(".js")]
    [InlineData(".ts")]
    [InlineData(".jsx")]
    [InlineData(".tsx")]
    [InlineData(".html")]
    [InlineData(".css")]
    [InlineData(".py")]
    [InlineData(".rs")]
    [InlineData(".java")]
    [InlineData(".cpp")]
    [InlineData(".go")]
    [InlineData(".php")]
    public void CodeFiles_ReturnCodeIcon(string ext)
    {
        var item = new FileSystemItem { Name = $"code{ext}", Path = $"/code{ext}", Type = FileSystemItemType.File, Extension = ext };
        Assert.Equal(Symbol.Code, _iconService.GetIcon(item));
    }

    // ================================================================
    // 11. JSON → code/data icon
    // ================================================================

    [Fact]
    public void JsonFile_ReturnsBracesVariableIcon()
    {
        var item = new FileSystemItem { Name = "config.json", Path = "/config.json", Type = FileSystemItemType.File, Extension = ".json" };
        var icon = _iconService.GetIcon(item);
        Assert.Equal(Symbol.BracesVariable, icon);
    }

    [Fact]
    public void XmlFile_ReturnsCodeIcon()
    {
        var item = new FileSystemItem { Name = "data.xml", Path = "/data.xml", Type = FileSystemItemType.File, Extension = ".xml" };
        Assert.Equal(Symbol.Code, _iconService.GetIcon(item));
    }

    [Fact]
    public void YamlFile_ReturnsCodeIcon()
    {
        var item = new FileSystemItem { Name = "config.yaml", Path = "/config.yaml", Type = FileSystemItemType.File, Extension = ".yaml" };
        Assert.Equal(Symbol.Code, _iconService.GetIcon(item));
    }

    // ================================================================
    // 12. README → appropriate icon (known filename)
    // ================================================================

    [Fact]
    public void ReadmeFile_ReturnsBookOpenIcon()
    {
        var item = new FileSystemItem { Name = "README", Path = "/README", Type = FileSystemItemType.File, Extension = "" };
        var icon = _iconService.GetIcon(item);
        Assert.Equal(Symbol.BookOpen, icon);
    }

    [Fact]
    public void ReadmeMd_ReturnsBookOpenIcon()
    {
        var item = new FileSystemItem { Name = "README.md", Path = "/README.md", Type = FileSystemItemType.File, Extension = ".md" };
        var icon = _iconService.GetIcon(item);
        Assert.Equal(Symbol.BookOpen, icon);
    }

    [Fact]
    public void License_ReturnsCertificateIcon()
    {
        var item = new FileSystemItem { Name = "LICENSE", Path = "/LICENSE", Type = FileSystemItemType.File, Extension = "" };
        Assert.Equal(Symbol.Certificate, _iconService.GetIcon(item));
    }

    [Fact]
    public void Dockerfile_ReturnsBoxIcon()
    {
        var item = new FileSystemItem { Name = "Dockerfile", Path = "/Dockerfile", Type = FileSystemItemType.File, Extension = "" };
        Assert.Equal(Symbol.Box, _iconService.GetIcon(item));
    }

    [Fact]
    public void Makefile_ReturnsWrenchIcon()
    {
        var item = new FileSystemItem { Name = "Makefile", Path = "/Makefile", Type = FileSystemItemType.File, Extension = "" };
        Assert.Equal(Symbol.Wrench, _iconService.GetIcon(item));
    }

    [Fact]
    public void Gitignore_ReturnsBranchForkIcon()
    {
        var item = new FileSystemItem { Name = ".gitignore", Path = "/.gitignore", Type = FileSystemItemType.File, Extension = "" };
        Assert.Equal(Symbol.BranchFork, _iconService.GetIcon(item));
    }

    [Fact]
    public void EditorConfig_ReturnsSettingsIcon()
    {
        var item = new FileSystemItem { Name = ".editorconfig", Path = "/.editorconfig", Type = FileSystemItemType.File, Extension = "" };
        Assert.Equal(Symbol.Settings, _iconService.GetIcon(item));
    }

    // ================================================================
    // 13. Unknown extension → generic file icon
    // ================================================================

    [Fact]
    public void UnknownExtension_ReturnsGenericDocumentIcon()
    {
        var item = new FileSystemItem { Name = "file.xyz123", Path = "/file.xyz123", Type = FileSystemItemType.File, Extension = ".xyz123" };
        var icon = _iconService.GetIcon(item);
        Assert.Equal(Symbol.Document, icon);
    }

    [Fact]
    public void NoExtension_UnknownName_ReturnsGenericDocumentIcon()
    {
        var item = new FileSystemItem { Name = "randomfile", Path = "/randomfile", Type = FileSystemItemType.File, Extension = "" };
        var icon = _iconService.GetIcon(item);
        Assert.Equal(Symbol.Document, icon);
    }

    // ================================================================
    // 14. Hidden file → resolves correctly (same icon, no error)
    // ================================================================

    [Fact]
    public void HiddenFile_ResolvesCorrectIcon()
    {
        var item = new FileSystemItem { Name = "secret.txt", Path = "/secret.txt", Type = FileSystemItemType.File, Extension = ".txt", IsHidden = true };
        var icon = _iconService.GetIcon(item);
        Assert.Equal(Symbol.DocumentText, icon);
    }

    [Fact]
    public void HiddenFolder_ResolvesCorrectIcon()
    {
        var item = new FileSystemItem { Name = ".hidden", Path = "/.hidden", Type = FileSystemItemType.Directory, IsHidden = true };
        var icon = _iconService.GetIcon(item);
        Assert.Equal(Symbol.Folder, icon);
    }

    // ================================================================
    // 15. Extension comparison is case insensitive
    // ================================================================

    [Theory]
    [InlineData(".PNG")]
    [InlineData(".Png")]
    [InlineData(".pNg")]
    [InlineData(".png")]
    public void ExtensionComparison_IsCaseInsensitive(string ext)
    {
        var item = new FileSystemItem { Name = $"image{ext}", Path = $"/image{ext}", Type = FileSystemItemType.File, Extension = ext };
        Assert.Equal(Symbol.Image, _iconService.GetIcon(item));
    }

    [Theory]
    [InlineData(".CS")]
    [InlineData(".Cs")]
    [InlineData(".cs")]
    public void CodeExtension_CaseInsensitive(string ext)
    {
        var item = new FileSystemItem { Name = $"file{ext}", Path = $"/file{ext}", Type = FileSystemItemType.File, Extension = ext };
        Assert.Equal(Symbol.Code, _iconService.GetIcon(item));
    }

    // ================================================================
    // Additional coverage: Spreadsheets, Databases, Shell scripts
    // ================================================================

    [Theory]
    [InlineData(".xls")]
    [InlineData(".xlsx")]
    [InlineData(".csv")]
    public void SpreadsheetFiles_ReturnTableIcon(string ext)
    {
        var item = new FileSystemItem { Name = $"data{ext}", Path = $"/data{ext}", Type = FileSystemItemType.File, Extension = ext };
        Assert.Equal(Symbol.Table, _iconService.GetIcon(item));
    }

    [Theory]
    [InlineData(".sql")]
    [InlineData(".db")]
    [InlineData(".sqlite")]
    public void DatabaseFiles_ReturnDatabaseIcon(string ext)
    {
        var item = new FileSystemItem { Name = $"data{ext}", Path = $"/data{ext}", Type = FileSystemItemType.File, Extension = ext };
        Assert.Equal(Symbol.Database, _iconService.GetIcon(item));
    }

    [Theory]
    [InlineData(".sh")]
    [InlineData(".bat")]
    [InlineData(".ps1")]
    [InlineData(".cmd")]
    public void ShellScripts_ReturnWindowConsoleIcon(string ext)
    {
        var item = new FileSystemItem { Name = $"script{ext}", Path = $"/script{ext}", Type = FileSystemItemType.File, Extension = ext };
        Assert.Equal(Symbol.WindowConsole, _iconService.GetIcon(item));
    }

    // ================================================================
    // Special types: Drive, SymbolicLink
    // ================================================================

    [Fact]
    public void DriveItem_ReturnsHardDriveIcon()
    {
        var item = new FileSystemItem { Name = "C:", Path = "C:\\", Type = FileSystemItemType.Drive };
        Assert.Equal(Symbol.HardDrive, _iconService.GetIcon(item));
    }

    [Fact]
    public void SymbolicLink_ReturnsLinkIcon()
    {
        var item = new FileSystemItem { Name = "link", Path = "/link", Type = FileSystemItemType.SymbolicLink };
        Assert.Equal(Symbol.Link, _iconService.GetIcon(item));
    }

    // ================================================================
    // App icons
    // ================================================================

    [Fact]
    public void AppIcons_BackForward_AreCorrect()
    {
        Assert.Equal(Symbol.ArrowLeft, _iconProvider.GetAppIcon("Back"));
        Assert.Equal(Symbol.ArrowRight, _iconProvider.GetAppIcon("Forward"));
        Assert.Equal(Symbol.ArrowUp, _iconProvider.GetAppIcon("Up"));
        Assert.Equal(Symbol.ArrowClockwise, _iconProvider.GetAppIcon("Refresh"));
    }

    [Fact]
    public void AppIcons_FileOperations_AreCorrect()
    {
        Assert.Equal(Symbol.Copy, _iconProvider.GetAppIcon("Copy"));
        Assert.Equal(Symbol.Cut, _iconProvider.GetAppIcon("Cut"));
        Assert.Equal(Symbol.ClipboardPaste, _iconProvider.GetAppIcon("Paste"));
        Assert.Equal(Symbol.Delete, _iconProvider.GetAppIcon("Delete"));
        Assert.Equal(Symbol.Rename, _iconProvider.GetAppIcon("Rename"));
    }

    [Fact]
    public void AppIcons_Sidebar_AreCorrect()
    {
        Assert.Equal(Symbol.Home, _iconProvider.GetAppIcon("Home"));
        Assert.Equal(Symbol.Desktop, _iconProvider.GetAppIcon("Desktop"));
        Assert.Equal(Symbol.ArrowDownload, _iconProvider.GetAppIcon("Downloads"));
        Assert.Equal(Symbol.Image, _iconProvider.GetAppIcon("Pictures"));
        Assert.Equal(Symbol.Video, _iconProvider.GetAppIcon("Videos"));
        Assert.Equal(Symbol.MusicNote2, _iconProvider.GetAppIcon("Music"));
        Assert.Equal(Symbol.HardDrive, _iconProvider.GetAppIcon("Drive"));
        Assert.Equal(Symbol.Globe, _iconProvider.GetAppIcon("Network"));
    }

    [Fact]
    public void AppIcons_UnknownName_ReturnsQuestion()
    {
        Assert.Equal(Symbol.Question, _iconProvider.GetAppIcon("NonExistentIcon"));
    }

    // ================================================================
    // Thumbnail eligibility
    // ================================================================

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".gif")]
    [InlineData(".bmp")]
    [InlineData(".webp")]
    [InlineData(".ico")]
    [InlineData(".tiff")]
    [InlineData(".tif")]
    public void ThumbnailEligibility_ImageFormats_ReturnsTrue(string ext)
    {
        var item = new FileSystemItem { Name = $"img{ext}", Path = $"/img{ext}", Type = FileSystemItemType.File, Extension = ext };
        Assert.True(_thumbnailService.CanGenerateThumbnail(item));
    }

    [Theory]
    [InlineData(".mp4")]
    [InlineData(".mp3")]
    [InlineData(".txt")]
    [InlineData(".pdf")]
    [InlineData(".zip")]
    [InlineData(".cs")]
    public void ThumbnailEligibility_NonImageFormats_ReturnsFalse(string ext)
    {
        var item = new FileSystemItem { Name = $"file{ext}", Path = $"/file{ext}", Type = FileSystemItemType.File, Extension = ext };
        Assert.False(_thumbnailService.CanGenerateThumbnail(item));
    }

    [Fact]
    public void ThumbnailEligibility_ShellIconFiles_OnWindows()
    {
        // On Windows, .lnk and .exe should be eligible for shell icon extraction
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            return; // Skip on non-Windows

        var lnk = new FileSystemItem { Name = "app.lnk", Path = "/app.lnk", Type = FileSystemItemType.File, Extension = ".lnk" };
        Assert.True(_thumbnailService.CanGenerateThumbnail(lnk));

        var exe = new FileSystemItem { Name = "app.exe", Path = "/app.exe", Type = FileSystemItemType.File, Extension = ".exe" };
        Assert.True(_thumbnailService.CanGenerateThumbnail(exe));

        var url = new FileSystemItem { Name = "site.url", Path = "/site.url", Type = FileSystemItemType.File, Extension = ".url" };
        Assert.True(_thumbnailService.CanGenerateThumbnail(url));
    }

    [Fact]
    public void ThumbnailEligibility_Directory_ReturnsFalse()
    {
        var item = new FileSystemItem { Name = "folder", Path = "/folder", Type = FileSystemItemType.Directory };
        Assert.False(_thumbnailService.CanGenerateThumbnail(item));
    }

    // ================================================================
    // Cache tests
    // ================================================================

    [Fact]
    public void Cache_Miss_ReturnsNull()
    {
        var cache = new ThumbnailCache(10);
        var item = new FileSystemItem { Name = "img.png", Path = "/img.png", Type = FileSystemItemType.File, LastModified = DateTime.Now };
        var cached = cache.TryGet(item, 64);
        Assert.Null(cached);
    }

    [Fact]
    public void Cache_Hit_ReturnsCachedBitmap()
    {
        var cache = new ThumbnailCache(10);
        var item = new FileSystemItem { Name = "img.png", Path = "/img.png", Type = FileSystemItemType.File, LastModified = DateTime.Now };
        var dummyBitmap = new Mock<IDisposable>().Object;

        cache.Add(item, 64, dummyBitmap);
        var cached = cache.TryGet(item, 64);

        Assert.NotNull(cached);
        Assert.Same(dummyBitmap, cached);
    }

    [Fact]
    public void Cache_DifferentSize_ReturnsNull()
    {
        var cache = new ThumbnailCache(10);
        var item = new FileSystemItem { Name = "img.png", Path = "/img.png", Type = FileSystemItemType.File, LastModified = DateTime.Now };
        var dummyBitmap = new Mock<IDisposable>().Object;

        cache.Add(item, 64, dummyBitmap);
        var cached = cache.TryGet(item, 128); // Different size

        Assert.Null(cached);
    }

    [Fact]
    public void Cache_Invalidation_RemovesAllSizes()
    {
        var cache = new ThumbnailCache(10);
        var item = new FileSystemItem { Name = "img.png", Path = "/img.png", Type = FileSystemItemType.File, LastModified = DateTime.Now };
        var dummy64 = new Mock<IDisposable>().Object;
        var dummy128 = new Mock<IDisposable>().Object;

        cache.Add(item, 64, dummy64);
        cache.Add(item, 128, dummy128);

        Assert.NotNull(cache.TryGet(item, 64));
        Assert.NotNull(cache.TryGet(item, 128));

        cache.Invalidate(item);

        Assert.Null(cache.TryGet(item, 64));
        Assert.Null(cache.TryGet(item, 128));
    }

    [Fact]
    public void Cache_LRUEviction_RemovesOldestItem()
    {
        var cache = new ThumbnailCache(maxItems: 2);
        var item1 = new FileSystemItem { Name = "a.png", Path = "/a.png", Type = FileSystemItemType.File, LastModified = DateTime.Now };
        var item2 = new FileSystemItem { Name = "b.png", Path = "/b.png", Type = FileSystemItemType.File, LastModified = DateTime.Now };
        var item3 = new FileSystemItem { Name = "c.png", Path = "/c.png", Type = FileSystemItemType.File, LastModified = DateTime.Now };

        var mockDisposable = new Mock<IDisposable>();

        cache.Add(item1, 64, mockDisposable.Object);
        cache.Add(item2, 64, new Mock<IDisposable>().Object);

        // Adding a 3rd should evict item1 (least recently used)
        cache.Add(item3, 64, new Mock<IDisposable>().Object);

        Assert.Null(cache.TryGet(item1, 64)); // Evicted
        Assert.NotNull(cache.TryGet(item2, 64));
        Assert.NotNull(cache.TryGet(item3, 64));

        // Verify dispose was called on evicted item
        mockDisposable.Verify(d => d.Dispose(), Times.Once);
    }

    [Fact]
    public void Cache_StaleEntry_IsReplaced()
    {
        var cache = new ThumbnailCache(10);
        var originalTime = new DateTime(2024, 1, 1);
        var newTime = new DateTime(2024, 6, 1);

        var item = new FileSystemItem { Name = "img.png", Path = "/img.png", Type = FileSystemItemType.File, LastModified = originalTime };
        var mockBitmap = new Mock<IDisposable>();
        cache.Add(item, 64, mockBitmap.Object);

        // Same path but newer modification time — cache should treat as stale
        var updatedItem = new FileSystemItem { Name = "img.png", Path = "/img.png", Type = FileSystemItemType.File, LastModified = newTime };
        var cached = cache.TryGet(updatedItem, 64);

        Assert.Null(cached); // Stale, should be removed
        mockBitmap.Verify(d => d.Dispose(), Times.Once);
    }

    // ================================================================
    // Known filenames — additional coverage
    // ================================================================

    [Theory]
    [InlineData("package.json", Symbol.Box)]
    [InlineData("package-lock.json", Symbol.BoxCheckmark)]
    [InlineData("tsconfig.json", Symbol.Code)]
    [InlineData("docker-compose.yml", Symbol.Box)]
    [InlineData("Cargo.toml", Symbol.Box)]
    public void KnownFilenames_ReturnExpectedIcons(string filename, Symbol expected)
    {
        var ext = System.IO.Path.GetExtension(filename);
        var item = new FileSystemItem { Name = filename, Path = $"/{filename}", Type = FileSystemItemType.File, Extension = ext };
        Assert.Equal(expected, _iconService.GetIcon(item));
    }
}
