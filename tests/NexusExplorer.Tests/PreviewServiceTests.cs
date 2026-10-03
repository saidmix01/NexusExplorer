using Microsoft.Extensions.Logging.Abstractions;
using NexusExplorer.Core.Models;
using NexusExplorer.Infrastructure.Preview;

namespace NexusExplorer.Tests;

public class PreviewServiceTests
{
    private readonly PreviewService _sut = new(NullLogger<PreviewService>.Instance);

    // --- CanPreview ---

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".gif")]
    [InlineData(".bmp")]
    [InlineData(".webp")]
    [InlineData(".svg")]
    public void CanPreview_ImageExtensions_ReturnsTrue(string ext)
    {
        var item = MakeFile("test" + ext);
        Assert.True(_sut.CanPreview(item));
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".md")]
    [InlineData(".log")]
    [InlineData(".csv")]
    [InlineData(".json")]
    [InlineData(".xml")]
    [InlineData(".yaml")]
    [InlineData(".yml")]
    [InlineData(".cs")]
    [InlineData(".js")]
    [InlineData(".ts")]
    [InlineData(".html")]
    [InlineData(".css")]
    [InlineData(".sql")]
    [InlineData(".rs")]
    [InlineData(".py")]
    [InlineData(".java")]
    [InlineData(".c")]
    [InlineData(".cpp")]
    [InlineData(".h")]
    public void CanPreview_TextExtensions_ReturnsTrue(string ext)
    {
        var item = MakeFile("test" + ext);
        Assert.True(_sut.CanPreview(item));
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".dll")]
    [InlineData(".zip")]
    [InlineData(".rar")]
    [InlineData(".mp4")]
    [InlineData(".mp3")]
    [InlineData(".iso")]
    [InlineData(".bin")]
    [InlineData(".docx")]
    [InlineData(".xlsx")]
    public void CanPreview_UnsupportedExtensions_ReturnsFalse(string ext)
    {
        var item = MakeFile("test" + ext);
        Assert.False(_sut.CanPreview(item));
    }

    [Fact]
    public void CanPreview_Directory_ReturnsTrue()
    {
        // Folders are now previewable: the preview lists their contents.
        var item = new FileSystemItem
        {
            Name = "SomeFolder",
            Path = @"C:\SomeFolder",
            Type = FileSystemItemType.Directory
        };
        Assert.True(_sut.CanPreview(item));
    }

    // --- GetPreviewType ---

    [Fact]
    public void GetPreviewType_PngFile_ReturnsImage()
    {
        var item = MakeFile("photo.png");
        Assert.Equal(PreviewType.Image, _sut.GetPreviewType(item));
    }

    [Fact]
    public void GetPreviewType_CsFile_ReturnsText()
    {
        var item = MakeFile("Program.cs");
        Assert.Equal(PreviewType.Text, _sut.GetPreviewType(item));
    }

    [Fact]
    public void GetPreviewType_ExeFile_ReturnsUnsupported()
    {
        var item = MakeFile("app.exe");
        Assert.Equal(PreviewType.Unsupported, _sut.GetPreviewType(item));
    }

    [Fact]
    public void GetPreviewType_Directory_ReturnsFolder()
    {
        var item = new FileSystemItem
        {
            Name = "Folder",
            Path = @"C:\Folder",
            Type = FileSystemItemType.Directory
        };
        Assert.Equal(PreviewType.Folder, _sut.GetPreviewType(item));
    }

    [Fact]
    public void GetPreviewType_CaseInsensitive()
    {
        var item = MakeFile("IMAGE.PNG");
        Assert.Equal(PreviewType.Image, _sut.GetPreviewType(item));
    }

    // --- GetPreviewAsync - Text ---

    [Fact]
    public async Task GetPreviewAsync_TextFile_ReturnsContent()
    {
        var tempFile = Path.GetTempFileName();
        var content = "Hello, World!\nLine 2";
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            var item = new FileSystemItem
            {
                Name = Path.GetFileName(tempFile),
                Path = tempFile,
                Type = FileSystemItemType.File,
                Extension = ".tmp",
                Size = new FileInfo(tempFile).Length
            };

            // Override extension for test — make a .txt file
            var txtFile = Path.ChangeExtension(tempFile, ".txt");
            File.Move(tempFile, txtFile);
            tempFile = txtFile;

            var txtItem = new FileSystemItem
            {
                Name = Path.GetFileName(txtFile),
                Path = txtFile,
                Type = FileSystemItemType.File,
                Extension = ".txt",
                Size = new FileInfo(txtFile).Length
            };

            var result = await _sut.GetPreviewAsync(txtItem);

            Assert.Equal(PreviewType.Text, result.Type);
            Assert.Equal(content, result.TextContent);
            Assert.NotNull(result.FileName);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GetPreviewAsync_LargeTextFile_ReturnsLimitMessage()
    {
        var item = new FileSystemItem
        {
            Name = "huge.txt",
            Path = @"C:\nonexistent\huge.txt",
            Type = FileSystemItemType.File,
            Extension = ".txt",
            Size = PreviewService.MaxTextPreviewBytes + 1
        };

        var result = await _sut.GetPreviewAsync(item);

        Assert.Equal(PreviewType.Text, result.Type);
        Assert.Contains("too large", result.TextContent);
    }

    // --- GetPreviewAsync - Image ---

    [Fact]
    public async Task GetPreviewAsync_ImageFile_ReturnsImagePath()
    {
        var item = new FileSystemItem
        {
            Name = "photo.png",
            Path = @"C:\photos\photo.png",
            Type = FileSystemItemType.File,
            Extension = ".png",
            Size = 1024
        };

        var result = await _sut.GetPreviewAsync(item);

        Assert.Equal(PreviewType.Image, result.Type);
        Assert.Equal(@"C:\photos\photo.png", result.ImagePath);
        Assert.Equal("photo.png", result.FileName);
    }

    // --- GetPreviewAsync - Unsupported ---

    [Fact]
    public async Task GetPreviewAsync_UnsupportedFile_ReturnsUnsupported()
    {
        var item = MakeFile("data.zip");

        var result = await _sut.GetPreviewAsync(item);

        Assert.Equal(PreviewType.Unsupported, result.Type);
        Assert.Equal("data.zip", result.FileName);
    }

    // --- Tab selection independence ---

    [Fact]
    public void TabSelection_Independent()
    {
        var tab1 = new TabItem(@"C:\A");
        var tab2 = new TabItem(@"C:\B");

        var file1 = MakeFile("a.txt");
        var file2 = MakeFile("b.txt");

        tab1.SelectedItem = file1;
        tab2.SelectedItem = file2;

        Assert.Equal("a.txt", tab1.SelectedItem.Name);
        Assert.Equal("b.txt", tab2.SelectedItem.Name);

        // Changing one doesn't affect the other
        tab1.SelectedItem = null;
        Assert.Null(tab1.SelectedItem);
        Assert.NotNull(tab2.SelectedItem);
    }

    // --- Helpers ---

    private static FileSystemItem MakeFile(string name)
    {
        var ext = Path.GetExtension(name);
        return new FileSystemItem
        {
            Name = name,
            Path = @"C:\test\" + name,
            Type = FileSystemItemType.File,
            Extension = ext,
            Size = 1024
        };
    }
}
