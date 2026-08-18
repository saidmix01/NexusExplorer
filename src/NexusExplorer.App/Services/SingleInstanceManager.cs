using System.IO.Pipes;

namespace NexusExplorer.App.Services;

/// <summary>
/// Ensures a single running Nexus Explorer instance and relays "activate" requests
/// from subsequent launches to the existing instance via a named pipe.
/// </summary>
public sealed class SingleInstanceManager : IDisposable
{
    private const string MutexName = @"Local\NexusExplorer_SingleInstance";
    private const string PipeName = "NexusExplorer_ActivationPipe";

    private readonly Mutex _mutex;
    private CancellationTokenSource? _cts;

    public bool IsFirstInstance { get; }

    /// <summary>Raised when another instance requests that this instance be shown.</summary>
    public event Action? ActivationRequested;

    public SingleInstanceManager()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsFirstInstance = createdNew;
    }

    /// <summary>
    /// Asks the already-running instance to show itself. Returns false if no listener
    /// is available (i.e. no other instance is actually running).
    /// </summary>
    public static bool RequestActivation()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1500);
            client.WriteByte(1);
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

            // The connection itself is the activation signal; drain the single byte.
            try { server.ReadByte(); } catch { }
            server.Dispose();

            ActivationRequested?.Invoke();
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _mutex.Dispose();
    }
}
