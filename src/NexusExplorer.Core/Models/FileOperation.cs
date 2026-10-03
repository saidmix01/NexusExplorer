using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NexusExplorer.Core.Models;

/// <summary>
/// Tracks the state of a single file operation (copy, move, delete, ...).
/// The operation is independent of any UI window: it owns its own cancellation
/// token and reports progress so it can continue in the background while the
/// File Operation Center is minimized or closed.
/// </summary>
public partial class FileOperation : ObservableObject
{
    private bool _cancellationRequested;

    public FileOperation(FileOperationType type, string? source, string? destination, string title)
    {
        OperationType = type;
        Source = source;
        Destination = destination;
        Title = title;
        CancelCommand = new RelayCommand(Cancel, () => CanCancel);
    }

    public Guid Id { get; } = Guid.NewGuid();

    public FileOperationType OperationType { get; }

    public string? Source { get; }

    public string? Destination { get; }

    public string Title { get; }

    [ObservableProperty]
    private FileOperationStatus _status = FileOperationStatus.Pending;

    [ObservableProperty]
    private string? _currentFile;

    [ObservableProperty]
    private int _currentFileIndex;

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private long _bytesProcessed;

    [ObservableProperty]
    private long _totalBytes;

    [ObservableProperty]
    private double _transferSpeed;

    [ObservableProperty]
    private TimeSpan? _estimatedRemaining;

    [ObservableProperty]
    private string? _errorMessage;

    public double ProgressPercentage
    {
        get
        {
            var raw = TotalBytes > 0
                ? (double)BytesProcessed / TotalBytes * 100
                : TotalFiles > 0
                    ? (double)CurrentFileIndex / TotalFiles * 100
                    : 0;
            // Clamp so a slightly-off byte total can never render as e.g. 20000%.
            return raw < 0 ? 0 : raw > 100 ? 100 : raw;
        }
    }

    public bool CanCancel => !_cancellationRequested && Status is FileOperationStatus.Pending or FileOperationStatus.Running;

    public bool IsCompleted => Status == FileOperationStatus.Completed;

    public bool IsCancelled => Status == FileOperationStatus.Cancelled;

    public bool HasError => Status == FileOperationStatus.Failed;

    public string StatusText => Status switch
    {
        FileOperationStatus.Pending => "Queued",
        FileOperationStatus.Running => CurrentFile ?? "Preparing...",
        FileOperationStatus.Completed => "Completed",
        FileOperationStatus.Cancelled => "Cancelled",
        FileOperationStatus.Failed => ErrorMessage ?? "Failed",
        FileOperationStatus.Paused => "Paused",
        _ => string.Empty
    };

    public string ProgressText => TotalBytes > 0
        ? $"{FormatSize(BytesProcessed)} / {FormatSize(TotalBytes)}"
        : string.Empty;

    public string SpeedText => TransferSpeed > 0
        ? $"{FormatSize((long)TransferSpeed)}/s"
        : string.Empty;

    public IRelayCommand CancelCommand { get; }

    internal CancellationTokenSource Cts { get; } = new();

    public CancellationToken CancellationToken => Cts.Token;

    /// <summary>
    /// Requests cancellation of this operation. The operation keeps reporting as
    /// running until the underlying service observes the cancellation and stops.
    /// </summary>
    public void Cancel()
    {
        if (!CanCancel) return;

        _cancellationRequested = true;
        Cts.Cancel();
        OnPropertyChanged(nameof(CanCancel));
        CancelCommand.NotifyCanExecuteChanged();
    }

    public void ApplyProgress(FileOperationProgress progress)
    {
        CurrentFile = progress.CurrentItem;
        CurrentFileIndex = progress.CurrentItemIndex;
        TotalFiles = progress.TotalItems;
        BytesProcessed = progress.BytesProcessed;
        TotalBytes = progress.TotalBytes;
        TransferSpeed = progress.BytesPerSecond;
        EstimatedRemaining = progress.EstimatedRemaining;
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(SpeedText));
        OnPropertyChanged(nameof(StatusText));
    }

    public void SetStatus(FileOperationStatus status)
    {
        Status = status;
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsCancelled));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(StatusText));
        CancelCommand.NotifyCanExecuteChanged();
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        if (bytes <= 0) return "0 B";

        var unitIndex = 0;
        var value = (double)bytes;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.#} {units[unitIndex]}";
    }
}
