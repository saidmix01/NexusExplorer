using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace NexusExplorer.Infrastructure.Logging;

/// <summary>
/// A single entry enqueued for writing.
/// </summary>
internal readonly record struct LogEntry(DateTimeOffset Timestamp, LogLevel Level, string Category, string Message, Exception? Exception);

/// <summary>
/// Thread-safe file logger provider. Loggers only enqueue formatted entries; a
/// single background consumer writes them sequentially to disk, so concurrent
/// log calls never corrupt the file and never block the UI thread.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly FileLoggerOptions _options;
    private readonly Channel<LogEntry> _queue;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _writerTask;
    private readonly object _writeLock = new();

    private StreamWriter? _writer;
    private string? _currentFile;
    private DateTime _currentFileDate;
    private int _currentFileIndex;
    private long _currentFileSize;
    private bool _disposed;

    public FileLoggerProvider(FileLoggerOptions options)
    {
        _options = options;
        _queue = Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _writerTask = Task.Run(ProcessQueueAsync);
    }

    public LogLevel MinimumLevel => _options.MinimumLevel;

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    internal void Enqueue(LogEntry entry)
    {
        // Drop silently if the queue has been completed during shutdown.
        _queue.Writer.TryWrite(entry);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _queue.Writer.TryComplete();
        try
        {
            _cts.CancelAfter(TimeSpan.FromSeconds(3));
            _writerTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Best-effort shutdown: never throw from Dispose.
        }
        _cts.Dispose();
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var entry in _queue.Reader.ReadAllAsync(_cts.Token))
            {
                WriteEntry(entry);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch
        {
            // Unexpected writer-loop error: never fault the background task.
        }

        // Drain anything still buffered before exiting.
        try
        {
            while (_queue.Reader.TryRead(out var remaining))
            {
                WriteEntry(remaining);
            }
        }
        catch
        {
            // Ignore drain errors.
        }

        CloseWriter();
    }

    private void WriteEntry(LogEntry entry)
    {
        try
        {
            lock (_writeLock)
            {
                var writer = GetWriter(entry.Timestamp);
                var text = Format(entry);
                writer.Write(text);
                writer.Flush();

                _currentFileSize += Encoding.UTF8.GetByteCount(text);
                if (_currentFileSize >= _options.MaxFileSizeBytes)
                {
                    _currentFileIndex++;
                    OpenWriter(entry.Timestamp);
                }
            }
        }
        catch
        {
            // Logging must never crash the application: drop a failed write
            // (disk full, permission denied, etc.).
        }
    }

    private StreamWriter GetWriter(DateTimeOffset timestamp)
    {
        var date = timestamp.Date;
        if (_writer is null || _currentFileDate != date)
        {
            _currentFileDate = date;
            _currentFileIndex = 0;
            OpenWriter(timestamp);
        }

        return _writer!;
    }

    private void OpenWriter(DateTimeOffset timestamp)
    {
        CloseWriter();
        Directory.CreateDirectory(_options.LogDirectory);

        var path = BuildPath(timestamp, _currentFileIndex);
        _currentFile = path;
        _currentFileSize = File.Exists(path) ? new FileInfo(path).Length : 0;

        // FileShare.Read lets the user open the current log while it is written.
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };

        CleanupOldLogs();
    }

    private string BuildPath(DateTimeOffset timestamp, int index)
    {
        var suffix = index == 0 ? string.Empty : $".{index}";
        return Path.Combine(_options.LogDirectory, $"{_options.FileNamePrefix}-{timestamp:yyyy-MM-dd}{suffix}.log");
    }

    private void CloseWriter()
    {
        try
        {
            _writer?.Dispose();
        }
        catch
        {
            // Ignore flush/dispose failures during shutdown.
        }
        _writer = null;
        _currentFile = null;
    }

    private void CleanupOldLogs()
    {
        try
        {
            var cutoff = DateTime.Now.Date.AddDays(-_options.RetainedDays);
            foreach (var file in Directory.EnumerateFiles(_options.LogDirectory, $"{_options.FileNamePrefix}-*.log"))
            {
                try
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                        File.Delete(file);
                }
                catch
                {
                    // Ignore locked or unreadable files.
                }
            }
        }
        catch
        {
            // Directory may not exist yet or be inaccessible.
        }
    }

    private static string Format(LogEntry entry)
    {
        var sb = new StringBuilder();
        sb.Append('[').Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append("] [")
          .Append(ToLevelString(entry.Level)).Append("] [")
          .Append(ToCategoryString(entry.Category)).Append("] ")
          .AppendLine(entry.Message);

        if (entry.Exception is not null)
        {
            sb.AppendLine("Exception:");
            sb.AppendLine(entry.Exception.ToString());
        }

        return sb.ToString();
    }

    private static string ToLevelString(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARNING",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRITICAL",
        _ => level.ToString().ToUpperInvariant()
    };

    private static string ToCategoryString(string category)
    {
        var dot = category.LastIndexOf('.');
        return dot >= 0 && dot < category.Length - 1 ? category[(dot + 1)..] : category;
    }
}

/// <summary>
/// A logger bound to a single category. Formats and enqueues entries without
/// performing any disk I/O on the calling thread.
/// </summary>
internal sealed class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly FileLoggerProvider _provider;

    public FileLogger(string categoryName, FileLoggerProvider provider)
    {
        _categoryName = categoryName;
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel)
        => logLevel != LogLevel.None && logLevel >= _provider.MinimumLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        _provider.Enqueue(new LogEntry(DateTimeOffset.Now, logLevel, _categoryName, message, exception));
    }
}
