using System.Text;
using NexusExplorer.Core.Abstractions;

namespace NexusExplorer.Platform.Windows.ConPty;

/// <summary>
/// A terminal session backed by Windows ConPTY.
/// Reads output asynchronously and exposes it via events.
/// </summary>
internal sealed class ConPtyTerminalSession : ITerminalSession
{
    private readonly ConPtyProcess _process;
    private readonly CancellationTokenSource _readCts = new();
    private readonly Task _readTask;
    private bool _disposed;

    public string Id { get; } = Guid.NewGuid().ToString();
    public string InitialWorkingDirectory { get; }
    public string CurrentWorkingDirectory { get; private set; }
    public bool IsRunning => !_disposed && _process.IsRunning;
    public int? ProcessId => _disposed ? null : _process.ProcessId;

    public event EventHandler<ReadOnlyMemory<byte>>? OutputReceived;
    public event EventHandler<int>? ProcessExited;

    internal ConPtyTerminalSession(ConPtyProcess process, string workingDirectory)
    {
        _process = process;
        InitialWorkingDirectory = workingDirectory;
        CurrentWorkingDirectory = workingDirectory;

        // Start reading output in the background
        _readTask = ReadOutputAsync(_readCts.Token);
    }

    public async Task WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ConPtyTerminalSession));
        await _process.InputStream.WriteAsync(data, cancellationToken);
        await _process.InputStream.FlushAsync(cancellationToken);
    }

    public Task WriteInputAsync(string text, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return WriteInputAsync(bytes.AsMemory(), cancellationToken);
    }

    public Task ResizeAsync(int columns, int rows, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ConPtyTerminalSession));
        _process.Resize((short)columns, (short)rows);
        return Task.CompletedTask;
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;

        _readCts.Cancel();

        // Give the process a moment to exit gracefully
        if (_process.IsRunning)
        {
            _process.Terminate();
        }

        try
        {
            await _readTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    private async Task ReadOutputAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead;
                try
                {
                    bytesRead = await _process.OutputStream.ReadAsync(buffer.AsMemory(), cancellationToken);
                }
                catch (IOException)
                {
                    // Pipe closed — process exited
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (bytesRead == 0)
                    break;

                // Copy the data and fire the event
                var data = new byte[bytesRead];
                buffer.AsSpan(0, bytesRead).CopyTo(data);
                OutputReceived?.Invoke(this, data.AsMemory());
            }
        }
        finally
        {
            // Notify exit
            var exitCode = _process.GetExitCode() ?? -1;
            ProcessExited?.Invoke(this, exitCode);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _readCts.Cancel();
        _readCts.Dispose();
        _process.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await CloseAsync();
        Dispose();
    }
}
