using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.Services.Thumbnails;

public sealed class ThumbnailCache
{
    private readonly int _maxItems;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly LinkedList<string> _lruList = new();
    private readonly object _lock = new();

    public ThumbnailCache(int maxItems = 1000)
    {
        _maxItems = maxItems;
    }

    public IDisposable? TryGet(FileSystemItem item, int size)
    {
        var key = GetKey(item, size);
        
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.LastModified == item.LastModified)
                {
                    _lruList.Remove(entry.Node);
                    _lruList.AddFirst(entry.Node);
                    return entry.Image;
                }
                else
                {
                    // Stale cache
                    _cache.TryRemove(key, out _);
                    _lruList.Remove(entry.Node);
                    entry.Image.Dispose();
                }
            }
        }
        return null;
    }

    public void Add(FileSystemItem item, int size, IDisposable image)
    {
        var key = GetKey(item, size);
        
        lock (_lock)
        {
            if (_cache.ContainsKey(key))
                return;

            if (_cache.Count >= _maxItems)
            {
                var lastNode = _lruList.Last;
                if (lastNode != null)
                {
                    if (_cache.TryRemove(lastNode.Value, out var removedEntry))
                    {
                        removedEntry.Image.Dispose();
                    }
                    _lruList.RemoveLast();
                }
            }

            var node = _lruList.AddFirst(key);
            _cache[key] = new CacheEntry(item.LastModified, image, node);
        }
    }

    public void Invalidate(FileSystemItem item)
    {
        lock (_lock)
        {
            var keysToRemove = new List<string>();
            foreach (var kvp in _cache)
            {
                if (kvp.Key.StartsWith(item.Path + "_", StringComparison.OrdinalIgnoreCase))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                if (_cache.TryRemove(key, out var entry))
                {
                    _lruList.Remove(entry.Node);
                    entry.Image.Dispose();
                }
            }
        }
    }

    private static string GetKey(FileSystemItem item, int size) => $"{item.Path}_{size}";

    private record CacheEntry(DateTime? LastModified, IDisposable Image, LinkedListNode<string> Node);
}