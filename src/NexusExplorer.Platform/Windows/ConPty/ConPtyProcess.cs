using System.Runtime.InteropServices;

namespace NexusExplorer.Platform.Windows.ConPty;

/// <summary>
/// Manages a process launched inside a ConPTY pseudo console.
/// Handles pipe creation, process startup, async I/O, and cleanup.
/// </summary>
internal sealed class ConPtyProcess : IDisposable
{
    private PseudoConsole? _pseudoConsole;
    private IntPtr _processHandle;
    private IntPtr _threadHandle;
    private IntPtr _attributeList;
    private FileStream? _inputStream;
    private FileStream? _outputStream;
    private bool _disposed;
    private int _processId;

    public int ProcessId => _processId;
    public FileStream InputStream => _inputStream ?? throw new ObjectDisposedException(nameof(ConPtyProcess));
    public FileStream OutputStream => _outputStream ?? throw new ObjectDisposedException(nameof(ConPtyProcess));

    public bool IsRunning
    {
        get
        {
            if (_disposed || _processHandle == IntPtr.Zero) return false;
            return NativeMethods.WaitForSingleObject(_processHandle, 0) != 0;
        }
    }

    public int? GetExitCode()
    {
        if (_processHandle == IntPtr.Zero) return null;
        if (NativeMethods.GetExitCodeProcess(_processHandle, out var exitCode))
            return (int)exitCode;
        return null;
    }

    /// <summary>
    /// Starts a process inside a ConPTY.
    /// </summary>
    public static ConPtyProcess Start(string commandLine, string workingDirectory, short columns, short rows)
    {
        var instance = new ConPtyProcess();

        try
        {
            // Create pipes using Win32 CreatePipe for SafeFileHandle compatibility
            var sa = new NativeMethods.SECURITY_ATTRIBUTES
            {
                nLength = Marshal.SizeOf<NativeMethods.SECURITY_ATTRIBUTES>(),
                bInheritHandle = 1,
                lpSecurityDescriptor = IntPtr.Zero
            };

            // Input pipe: we write to inputWriteSide, ConPTY reads from inputReadSide
            if (!NativeMethods.CreatePipe(out var inputReadSide, out var inputWriteSide, ref sa, 0))
                throw new InvalidOperationException($"CreatePipe failed for input: {Marshal.GetLastWin32Error()}");
            
            // Output pipe: ConPTY writes to outputWriteSide, we read from outputReadSide
            if (!NativeMethods.CreatePipe(out var outputReadSide, out var outputWriteSide, ref sa, 0))
                throw new InvalidOperationException($"CreatePipe failed for output: {Marshal.GetLastWin32Error()}");

            // Create pseudo console
            instance._pseudoConsole = PseudoConsole.Create(inputReadSide, outputWriteSide, columns, rows);

            // Close the sides given to ConPTY — it owns them now
            inputReadSide.Dispose();
            outputWriteSide.Dispose();

            // Our I/O streams
            instance._inputStream = new FileStream(inputWriteSide, FileAccess.Write);
            instance._outputStream = new FileStream(outputReadSide, FileAccess.Read);

            // Create process with pseudo console
            instance.StartProcess(commandLine, workingDirectory);

            return instance;
        }
        catch
        {
            instance.Dispose();
            throw;
        }
    }

    private void StartProcess(string commandLine, string workingDirectory)
    {
        // Initialize thread attribute list
        var size = IntPtr.Zero;
        NativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);

        _attributeList = Marshal.AllocHGlobal(size.ToInt32());
        if (!NativeMethods.InitializeProcThreadAttributeList(_attributeList, 1, 0, ref size))
            throw new InvalidOperationException($"InitializeProcThreadAttributeList failed: {Marshal.GetLastWin32Error()}");

        // Set the pseudo console attribute
        if (!NativeMethods.UpdateProcThreadAttribute(
            _attributeList,
            0,
            (IntPtr)NativeMethods.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
            _pseudoConsole!.Handle,
            (IntPtr)IntPtr.Size,
            IntPtr.Zero,
            IntPtr.Zero))
        {
            throw new InvalidOperationException($"UpdateProcThreadAttribute failed: {Marshal.GetLastWin32Error()}");
        }

        // Create process
        var startupInfo = new NativeMethods.StartupInfoEx
        {
            StartupInfo = new NativeMethods.StartupInfo { cb = Marshal.SizeOf<NativeMethods.StartupInfoEx>() },
            lpAttributeList = _attributeList
        };

        if (!NativeMethods.CreateProcessW(
            null,
            commandLine,
            IntPtr.Zero,
            IntPtr.Zero,
            false,
            NativeMethods.EXTENDED_STARTUPINFO_PRESENT | NativeMethods.CREATE_UNICODE_ENVIRONMENT,
            IntPtr.Zero,
            workingDirectory,
            in startupInfo,
            out var processInfo))
        {
            throw new InvalidOperationException($"CreateProcessW failed: {Marshal.GetLastWin32Error()}");
        }

        _processHandle = processInfo.hProcess;
        _threadHandle = processInfo.hThread;
        _processId = processInfo.dwProcessId;
    }

    /// <summary>
    /// Resizes the pseudo console.
    /// </summary>
    public void Resize(short columns, short rows)
    {
        _pseudoConsole?.Resize(columns, rows);
    }

    /// <summary>
    /// Terminates the process if still running.
    /// </summary>
    public void Terminate()
    {
        if (_processHandle != IntPtr.Zero && IsRunning)
        {
            NativeMethods.TerminateProcess(_processHandle, 1);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _inputStream?.Dispose();
        _inputStream = null;
        _outputStream?.Dispose();
        _outputStream = null;

        _pseudoConsole?.Dispose();
        _pseudoConsole = null;

        if (_attributeList != IntPtr.Zero)
        {
            NativeMethods.DeleteProcThreadAttributeList(_attributeList);
            Marshal.FreeHGlobal(_attributeList);
            _attributeList = IntPtr.Zero;
        }

        if (_threadHandle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(_threadHandle);
            _threadHandle = IntPtr.Zero;
        }

        if (_processHandle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
        }
    }
}
