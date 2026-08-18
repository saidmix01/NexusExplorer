using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.Services.Thumbnails;

/// <summary>
/// Generates thumbnails for image files and extracts shell icons for shortcuts/executables.
/// Uses an LRU cache and concurrency limiting.
/// </summary>
public sealed class ThumbnailService : IThumbnailService
{
    private readonly ThumbnailCache _cache;
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger<ThumbnailService> _logger;

    // Image extensions that can be decoded as thumbnails
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico", ".tiff", ".tif"
    };

    public ThumbnailService(ILogger<ThumbnailService>? logger = null)
    {
        _cache = new ThumbnailCache(maxItems: 500);
        _semaphore = new SemaphoreSlim(Environment.ProcessorCount * 2);
        _logger = logger ?? NullLogger<ThumbnailService>.Instance;
    }

    public bool CanGenerateThumbnail(FileSystemItem item)
    {
        if (item.Type is not (FileSystemItemType.File or FileSystemItemType.SymbolicLink))
            return false;

        var ext = (item.Extension ?? Path.GetExtension(item.Name))?.ToLowerInvariant();
        if (ext is null) return false;

        // Image files — decode as thumbnail
        if (ImageExtensions.Contains(ext))
            return true;

        // On Windows, extract shell icons for shortcuts and executables
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (WindowsShellIconExtractor.ShouldExtractShellIcon(ext))
                return true;
        }

        return false;
    }

    public async Task<object?> GetThumbnailAsync(FileSystemItem item, int size, CancellationToken cancellationToken = default)
    {
        if (!CanGenerateThumbnail(item))
            return null;

        var cached = _cache.TryGet(item, size);
        if (cached != null)
        {
            _logger.LogDebug("Thumbnail cache hit: {Path}", item.Path);
            return cached;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            cached = _cache.TryGet(item, size);
            if (cached != null)
            {
                _logger.LogDebug("Thumbnail cache hit: {Path}", item.Path);
                return cached;
            }

            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Thumbnail cache miss: {Path}", item.Path);

            var ext = (item.Extension ?? Path.GetExtension(item.Name))?.ToLowerInvariant();
            Bitmap? bitmap = null;

            if (ext is not null && ImageExtensions.Contains(ext))
            {
                // Standard image decoding
                bitmap = await Task.Run(() => DecodeImage(item.Path, size, cancellationToken), cancellationToken);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                     && WindowsShellIconExtractor.ShouldExtractShellIcon(ext))
            {
                // Windows shell icon extraction
                bitmap = await WindowsShellIconExtractor.ExtractIconAsync(item.Path, size, cancellationToken);
            }

            if (bitmap != null)
            {
                _cache.Add(item, size, bitmap);
            }

            return bitmap;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Thumbnail generation failed: {Path}", item.Path);
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void InvalidateCache(FileSystemItem item)
    {
        _cache.Invalidate(item);
    }

    private static Bitmap? DecodeImage(string path, int targetSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(path))
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length < 8)
                return null;

            cancellationToken.ThrowIfCancellationRequested();
            return Bitmap.DecodeToWidth(stream, targetSize);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
