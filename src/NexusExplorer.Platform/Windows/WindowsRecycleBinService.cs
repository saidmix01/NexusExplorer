using System.Runtime.InteropServices;
using NexusExplorer.Core.Abstractions;

namespace NexusExplorer.Platform.Windows;

/// <summary>
/// Windows Recycle Bin implementation using FileOperationAPI (COM IFileOperation).
/// Falls back to Microsoft.VisualBasic.FileIO for simplicity and reliability.
/// </summary>
public sealed class WindowsRecycleBinService : IRecycleBinService
{
    public bool IsSupported => true;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    private const int FO_DELETE = 0x0003;
    private const int FOF_ALLOWUNDO = 0x0040;
    private const int FOF_NOCONFIRMATION = 0x0010;
    private const int FOF_SILENT = 0x0004;
    private const int FOF_NOERRORUI = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public int wFunc;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pTo;
        public int fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszProgressTitle;
    }

    public Task<bool> RecycleAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                // SHFileOperation requires double-null terminated string
                var fileOp = new SHFILEOPSTRUCT
                {
                    wFunc = FO_DELETE,
                    pFrom = path + '\0',
                    fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
                };

                var result = SHFileOperation(ref fileOp);
                return result == 0 && !fileOp.fAnyOperationsAborted;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }
}
