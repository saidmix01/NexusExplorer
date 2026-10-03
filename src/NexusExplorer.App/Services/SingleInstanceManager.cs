using System.IO.Pipes;
using System.Text;

namespace NexusExplorer.App.Services;

/// <summary>
/// Ensures a single running Nexus Explorer instance and relays activation requests
/// (optionally carrying a path to open) from subsequent launches to the existing
/// instance via a named pipe.
/// </summary>
public sealed class SingleInstanceManager : IDisposable
{
    private const string MutexName = @"Local\NexusExplorer_SingleInstance";
    private const string PipeName = "NexusExplorer_ActivationPipe";

    private readonly Mutex _mutex;
    private CancellationTokenSource? _cts;

    public bool IsFirstInstance { get; }

    /// <summary>
    /// Raised when another instance requests activation. The string argument is the path the
    /// new launch wants to open, or null/empty if it only asked to show the window.
    /// </summary>
    public event Action<string?>? ActivationRequested;

    public SingleInstanceManager()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsFirstInstance = createdNew;
    }

    /// <summary>
    /// Asks the already-running instance to show itself and, optionally, navigate to
    /// <paramref name="path"/>. Returns false if no listener is available.
    /// </summary>
    public static bool RequestActivation(string? path = null)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1500);

            var payload = Encoding.UTF8.GetBytes(path ?? string.Empty);
            client.Write(payload, 0, payload.Length);
            client.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void StartListening()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => ListenAsync(_cts.Token));
    }

    private async Task ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var server = new NamedPipeServerStream(
                PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(token);
            }
            catch
            {
                server.Dispose();
                break;
            }

            string? path = null;
            try
            {
                using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: false);
                var text = await reader.ReadToEndAsync(token);
                path = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            }
            catch
            {
                server.Dispose();
            }

            ActivationRequested?.Invoke(path);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _mutex.Dispose();
    }
}
