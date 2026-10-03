using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace NexusExplorer.Platform.Windows;

/// <summary>
/// COM interop declarations for invoking the native Windows shell context menu
/// (IShellFolder / IContextMenu family). These mirror the standard Win32 shell API.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ShellNative
{
    // --- shell32 / ole32 ---

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SHParseDisplayName(
        string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

    [DllImport("shell32.dll")]
    internal static extern int SHBindToParent(
        IntPtr pidl, ref Guid riid, out IntPtr ppv, out IntPtr ppidlLast);

    [DllImport("ole32.dll")]
    internal static extern void CoTaskMemFree(IntPtr pv);

    // --- user32 (menu) ---

    [DllImport("user32.dll")]
    internal static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    internal static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    internal static extern uint TrackPopupMenuEx(
        IntPtr hMenu, uint uFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    internal const uint TPM_RETURNCMD = 0x0100;
    internal const uint TPM_LEFTALIGN = 0x0000;
    internal const uint TPM_RIGHTBUTTON = 0x0002;

    // QueryContextMenu flags
    internal const uint CMF_NORMAL = 0x00000000;
    internal const uint CMF_EXPLORE = 0x00000004;
    internal const uint CMF_EXTENDEDVERBS = 0x00000100;

    // GetCommandString / InvokeCommand
    internal const uint GCS_VERBW = 0x00000004;

    internal static readonly Guid IID_IShellFolder =
        new("000214E6-0000-0000-C000-000000000046");
    internal static readonly Guid IID_IContextMenu =
        new("000214E4-0000-0000-C000-000000000046");

    [StructLayout(LayoutKind.Sequential)]
    internal struct CMINVOKECOMMANDINFOEX
    {
        public int cbSize;
        public int fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        [MarshalAs(UnmanagedType.LPStr)] public string? lpParameters;
        [MarshalAs(UnmanagedType.LPStr)] public string? lpDirectory;
        public int nShow;
        public int dwHotKey;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.LPStr)] public string? lpTitle;
        public IntPtr lpVerbW;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpParametersW;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpDirectoryW;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpTitleW;
        public int ptInvokeX;
        public int ptInvokeY;
    }
}

[SupportedOSPlatform("windows")]
[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("000214E6-0000-0000-C000-000000000046")]
internal interface IShellFolder
{
    // Only GetUIObjectOf is used; the earlier slots must be declared to preserve the vtable order.
    [PreserveSig] int ParseDisplayName(IntPtr hwnd, IntPtr pbc, string pszDisplayName,
        out uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
    [PreserveSig] int EnumObjects(IntPtr hwnd, int grfFlags, out IntPtr ppenumIDList);
    [PreserveSig] int BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
    [PreserveSig] int BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
    [PreserveSig] int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
    [PreserveSig] int CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr ppv);
    [PreserveSig] int GetAttributesOf(uint cidl, IntPtr[] apidl, ref uint rgfInOut);
    [PreserveSig] int GetUIObjectOf(IntPtr hwndOwner, uint cidl, IntPtr[] apidl,
        ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);
    [PreserveSig] int GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);
    [PreserveSig] int SetNameOf(IntPtr hwnd, IntPtr pidl, string pszName, uint uFlags, out IntPtr ppidlOut);
}

[SupportedOSPlatform("windows")]
[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("000214E4-0000-0000-C000-000000000046")]
internal interface IContextMenu
{
    [PreserveSig] int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
    [PreserveSig] int InvokeCommand(ref ShellNative.CMINVOKECOMMANDINFOEX pici);
    [PreserveSig] int GetCommandString(IntPtr idCmd, uint uType, IntPtr pReserved,
        [MarshalAs(UnmanagedType.LPArray)] byte[] commandString, int cchMax);
}

[SupportedOSPlatform("windows")]
[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("000214f4-0000-0000-c000-000000000046")]
internal interface IContextMenu2
{
    [PreserveSig] int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
    [PreserveSig] int InvokeCommand(ref ShellNative.CMINVOKECOMMANDINFOEX pici);
    [PreserveSig] int GetCommandString(IntPtr idCmd, uint uType, IntPtr pReserved,
        [MarshalAs(UnmanagedType.LPArray)] byte[] commandString, int cchMax);
    [PreserveSig] int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
}

[SupportedOSPlatform("windows")]
[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("bcfce0a0-ec17-11d0-8d10-00a0c90f2719")]
internal interface IContextMenu3
{
    [PreserveSig] int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
    [PreserveSig] int InvokeCommand(ref ShellNative.CMINVOKECOMMANDINFOEX pici);
    [PreserveSig] int GetCommandString(IntPtr idCmd, uint uType, IntPtr pReserved,
        [MarshalAs(UnmanagedType.LPArray)] byte[] commandString, int cchMax);
    [PreserveSig] int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
    [PreserveSig] int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);
}
