using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Infrastructure.FileOperations;

/// <summary>
/// Default <see cref="IFileOperationManager"/> implementation. It owns the
/// lifecycle of every file operation: it creates the operation, maps progress
/// reports into it, observes completion/cancellation/failure and keeps track of
/// the underlying tasks so they can be awaited during a graceful shutdown.
/// </summary>
public sealed class FileOperationManager : IFileOperationManager
{
    private readonly IFileOperationService _fileOperationService;
    private readonly ICompressionService _compressionService;
    private readonly ISendToService _sendToService;
    private readonly ConcurrentDictionary<Guid, Task> _runningTasks = new();

    public FileOperationManager(
        IFileOperationService fileOperationService,
        ICompressionService compressionService,
        ISendToService sendToService)
    {
        _fileOperationService = fileOperationService;
        _compressionService = compressionService;
        _sendToService = sendToService;
    }

    public ObservableCollection<FileOperation> Operations { get; } = [];

    public bool HasActiveOperations =>
        Operations.Any(o => o.Status is FileOperationStatus.Pending or FileOperationStatus.Running);

    public event EventHandler? Changed;

    public Task<FileOperationResult> CopyAsync(
        IReadOnlyList<string> sources,
        string destination,
        string title,
        Func<FileConflict, Task<ConflictAction>>? conflictResolver = null) =>
        RunAsync(
            FileOperationType.Copy,
            DescribeSource(sources),
            DescribeDestination(destination),
            title,
            (progress, ct) => _fileOperationService.CopyAsync(sources, destination, progress, conflictResolver, ct));

    public Task<FileOperationResult> MoveAsync(
        IReadOnlyList<string> sources,
        string destination,
        string title,
        Func<FileConflict, Task<ConflictAction>>? conflictResolver = null) =>
        RunAsync(
            FileOperationType.Move,
            DescribeSource(sources),
            DescribeDestination(destination),
            title,
            (progress, ct) => _fileOperationService.MoveAsync(sources, destination, progress, conflictResolver, ct));

    public Task<FileOperationResult> DeleteAsync(
        IReadOnlyList<string> paths,
        bool useRecycleBin,
        string title) =>
        RunAsync(
            FileOperationType.Delete,
            DescribeSource(paths),
            useRecycleBin ? "Recycle Bin" : null,
            title,
            (progress, ct) => _fileOperationService.DeleteAsync(paths, useRecycleBin, progress, ct));

    public Task<FileOperationResult> SendToAsync(
        IReadOnlyList<string> sourcePaths,
        SendToTarget target,
        string title,
        Func<FileConflict, Task<ConflictAction>>? conflictResolver = null) =>
        RunAsync(
            FileOperationType.Copy, // Send To is a copy to a known target
            DescribeSource(sourcePaths),
            target.Name,
            title,
            (progress, ct) => _sendToService.SendToAsync(sourcePaths, target, progress, conflictResolver, ct));

    public Task<CompressionResult> CompressAsync(
        IReadOnlyList<string> sourcePaths,
        string title) =>
        RunCompressionAsync(
            FileOperationType.Compress,
            DescribeSource(sourcePaths),
            null,
            title,
            (progress, ct) => _compressionService.CompressAsync(sourcePaths, progress, ct));

    public Task<CompressionResult> ExtractAsync(
        string archivePath,
        string? destinationDirectory,
        string title) =>
        RunCompressionAsync(
            FileOperationType.Extract,
            Path.GetFileName(archivePath),
            DescribeDestination(destinationDirectory),
            title,
            (progress, ct) => _compressionService.ExtractAsync(archivePath, destinationDirectory, progress, ct));

    /// <summary>
    /// Runs a compression/extraction as a managed operation. Adapts the CompressionResult to the
    /// operation's status so it shows up in the File Operation Center like every other operation.
    /// </summary>
    private async Task<CompressionResult> RunCompressionAsync(
        FileOperationType type,
        string? source,
        string? destination,
        string title,
        Func<IProgress<FileOperationProgress>, CancellationToken, Task<CompressionResult>> execute)
    {
        var operation = new FileOperation(type, source, destination, title);
        operation.PropertyChanged += OnOperationPropertyChanged;
        Operations.Add(operation);
        RaiseChanged();

        operation.SetStatus(FileOperationStatus.Running);

        var progress = new Progress<FileOperationProgress>(operation.ApplyProgress);
        var task = execute(progress, operation.CancellationToken);
        _runningTasks[operation.Id] = task;

        CompressionResult result;
        try
        {
            result = await task;
        }
        catch (OperationCanceledException)
        {
            result = CompressionResult.CancelledResult();
        }
        catch (Exception ex)
        {
            result = CompressionResult.Failed(ex.Message);
        }

        // Map CompressionResult -> operation status via the shared FinishOperation path.
        FinishOperation(operation, ToFileOperationResult(result));
        return result;
    }

    private static FileOperationResult ToFileOperationResult(CompressionResult r)
    {
        if (r.Cancelled) return FileOperationResult.CancelledResult();
        if (!r.Success) return FileOperationResult.Failed(r.Error ?? "Operation failed.");
        return FileOperationResult.Ok(r.ItemsProcessed);
    }

    public void CancelOperation(FileOperation operation) => operation.Cancel();

    public void CancelAll()
    {
        foreach (var operation in Operations)
            operation.Cancel();
    }

    public async Task CancelAllAndWaitAsync()
    {
        CancelAll();
        await WaitForAllAsync();
    }

    public async Task WaitForAllAsync()
    {
        var tasks = _runningTasks.Values.ToArray();
        if (tasks.Length == 0) return;

        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            // Individual failures are already captured on their FileOperation.
        }
    }

    private async Task<FileOperationResult> RunAsync(
        FileOperationType type,
        string? source,
        string? destination,
        string title,
        Func<IProgress<FileOperationProgress>, CancellationToken, Task<FileOperationResult>> execute)
    {
        var operation = new FileOperation(type, source, destination, title);
        operation.PropertyChanged += OnOperationPropertyChanged;
        Operations.Add(operation);
        RaiseChanged();

        operation.SetStatus(FileOperationStatus.Running);

        var progress = new Progress<FileOperationProgress>(operation.ApplyProgress);
        var task = execute(progress, operation.CancellationToken);
        _runningTasks[operation.Id] = task;

        FileOperationResult result;
        try
        {
            result = await task;
        }
        catch (OperationCanceledException)
        {
            result = FileOperationResult.CancelledResult();
        }
        catch (Exception ex)
        {
            result = FileOperationResult.Failed(ex.Message);
        }

        FinishOperation(operation, result);
        return result;
    }

    private void FinishOperation(FileOperation operation, FileOperationResult result)
    {
        if (result.Cancelled)
        {
            operation.SetStatus(FileOperationStatus.Cancelled);
        }
        else if (!result.Success)
        {
            operation.ErrorMessage = result.Error;
            operation.SetStatus(FileOperationStatus.Failed);
        }
        else
        {
            operation.SetStatus(FileOperationStatus.Completed);
        }

        _runningTasks.TryRemove(operation.Id, out _);
        RaiseChanged();

        _ = RemoveAfterDelayAsync(operation);
    }

    private async Task RemoveAfterDelayAsync(FileOperation operation)
    {
        try
        {
            await Task.Delay(2500);
        }
        catch
        {
            // Delay interruption is non-critical.
        }

        Operations.Remove(operation);
        operation.PropertyChanged -= OnOperationPropertyChanged;
        RaiseChanged();
    }

    private void OnOperationPropertyChanged(object? sender, PropertyChangedEventArgs e) => RaiseChanged();

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static string? DescribeSource(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return null;
        if (paths.Count == 1) return Path.GetFileName(paths[0]);
        return $"{paths.Count} items";
    }

    private static string? DescribeDestination(string? destination)
    {
        if (string.IsNullOrWhiteSpace(destination)) return null;

        var trimmed = destination.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(trimmed);
    }
}
