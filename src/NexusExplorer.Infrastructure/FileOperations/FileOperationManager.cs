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
    private readonly ConcurrentDictionary<Guid, Task> _runningTasks = new();

    public FileOperationManager(IFileOperationService fileOperationService)
    {
        _fileOperationService = fileOperationService;
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

    public void CancelOperation(FileOperation operation) => operation.Cancel();

    public void CancelAll()
    {
        foreach (var operation in Operations)
            operation.Cancel();
    }

    public async Task CancelAllAndWaitAsync()
    {
        CancelAll();

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
