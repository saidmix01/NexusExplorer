using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NexusExplorer.Platform.Windows.ConPty;

/// <summary>
/// Manages the lifecycle of a Windows Pseudo Console (ConPTY).
/// </summary>
internal sealed class PseudoConsole : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    public IntPtr Handle => _handle;

    private PseudoConsole(IntPtr handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Creates a new pseudo console with the given size and pipe handles.
    /// </summary>
    public static PseudoConsole Create(SafeFileHandle inputReadSide, SafeFileHandle outputWriteSide, short columns, short rows)
    {
        var size = new NativeMethods.Coord(columns, rows);
        var result = NativeMethods.CreatePseudoConsole(size, inputReadSide, outputWriteSide, 0, out var handle);

        if (result != 0)
            throw new InvalidOperationException($"CreatePseudoConsole failed with HRESULT: 0x{result:X8}");

        return new PseudoConsole(handle);
    }

    /// <summary>
    /// Resizes the pseudo console.
    /// </summary>
    public void Resize(short columns, short rows)
    {
        if (_disposed) return;
        var size = new NativeMethods.Coord(columns, rows);
        NativeMethods.ResizePseudoConsole(_handle, size);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.ClosePseudoConsole(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
