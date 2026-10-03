using System.Runtime.InteropServices;
using System.Text;
using NexusExplorer.Core.Abstractions;

namespace NexusExplorer.Platform.Windows;

/// <summary>
/// Enriches the "Details" tab on Windows using the Shell Property System through the
/// high-level <c>IShellItem2</c> API (GetString), which returns display-ready strings and
/// avoids manual PROPVARIANT marshalling (a common source of native crashes).
/// Fails safe: any error yields no extra data.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class WindowsShellPropertyProvider : IShellMetadataProvider
{
    public IReadOnlyList<DetailGroup> GetExtraDetails(string path)
    {
        var groups = new List<DetailGroup>();
        IShellItem2? item = null;
        try
        {
            var iid = typeof(IShellItem2).GUID;
            var hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out item);
            if (hr != 0 || item is null)
                return groups;

            AddGroup(groups, item, "Description", new (string, PropertyKey)[]
            {
                ("Title", PropertyKeys.Title),
                ("Subject", PropertyKeys.Subject),
                ("Tags", PropertyKeys.Keywords),
                ("Comments", PropertyKeys.Comment),
            });

            AddGroup(groups, item, "Origin", new (string, PropertyKey)[]
            {
                ("Authors", PropertyKeys.Author),
                ("Date taken", PropertyKeys.DateTaken),
            });

            AddGroup(groups, item, "Image", new (string, PropertyKey)[]
            {
                ("Dimensions", PropertyKeys.Dimensions),
                ("Width", PropertyKeys.ImageWidth),
                ("Height", PropertyKeys.ImageHeight),
                ("Bit depth", PropertyKeys.BitDepth),
            });

            AddGroup(groups, item, "Camera", new (string, PropertyKey)[]
            {
                ("Camera maker", PropertyKeys.CameraManufacturer),
                ("Camera model", PropertyKeys.CameraModel),
            });

            AddGroup(groups, item, "Media", new (string, PropertyKey)[]
            {
                ("Length", PropertyKeys.Duration),
                ("Frame width", PropertyKeys.FrameWidth),
                ("Frame height", PropertyKeys.FrameHeight),
                ("Total bitrate", PropertyKeys.TotalBitrate),
            });

            AddGroup(groups, item, "Audio", new (string, PropertyKey)[]
            {
                ("Artist", PropertyKeys.MusicArtist),
                ("Album", PropertyKeys.MusicAlbum),
                ("Genre", PropertyKeys.MusicGenre),
            });

            AddGroup(groups, item, "File", new (string, PropertyKey)[]
            {
                ("File version", PropertyKeys.FileVersion),
                ("Product name", PropertyKeys.ProductName),
                ("Product version", PropertyKeys.ProductVersion),
                ("Copyright", PropertyKeys.Copyright),
            });
        }
        catch
        {
            // Best effort — return whatever was gathered.
        }
        finally
        {
            if (item is not null)
                Marshal.ReleaseComObject(item);
        }

        return groups;
    }

    public string? GetOpensWith(string path)
    {
        try
        {
            var ext = System.IO.Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) return null;

            uint length = 0;
            AssocQueryString(0, AssocStr.FriendlyAppName, ext, null, null, ref length);
            if (length == 0) return null;

            var sb = new StringBuilder((int)length);
            var res = AssocQueryString(0, AssocStr.FriendlyAppName, ext, null, sb, ref length);
            return res == 0 ? sb.ToString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static void AddGroup(List<DetailGroup> groups, IShellItem2 item, string groupName,
        (string Name, PropertyKey Key)[] keys)
    {
        var props = new List<DetailProperty>();
        foreach (var (name, key) in keys)
        {
            var value = ReadString(item, key);
            if (!string.IsNullOrWhiteSpace(value))
                props.Add(new DetailProperty { Name = name, Value = value! });
        }
        if (props.Count > 0)
            groups.Add(new DetailGroup { Name = groupName, Properties = props });
    }

    /// <summary>
    /// Reads a property as a formatted display string via IShellItem2.GetString.
    /// Returns null when the property is absent (GetString fails), which is expected and safe.
    /// </summary>
    private static string? ReadString(IShellItem2 item, PropertyKey key)
    {
        try
        {
            var local = key;
            var hr = item.GetString(ref local, out var value);
            return hr == 0 ? value : null;
        }
        catch
        {
            return null;
        }
    }

    // ---- P/Invoke ----

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem2 ppv);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern uint AssocQueryString(uint flags, AssocStr str, string pszAssoc,
        string? pszExtra, StringBuilder? pszOut, ref uint pcchOut);

    private enum AssocStr { FriendlyAppName = 4 }

    [ComImport, Guid("7e9fb0d3-919f-4307-ab2e-9b1860310c93"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem2
    {
        // IShellItem
        [PreserveSig] int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int GetParent(out IShellItem2 ppsi);
        [PreserveSig] int GetDisplayName(uint sigdnName, out IntPtr ppszName);
        [PreserveSig] int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        [PreserveSig] int Compare(IShellItem2 psi, uint hint, out int piOrder);
        // IShellItem2 (only GetString is needed)
        [PreserveSig] int GetPropertyStore(uint flags, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int GetPropertyStoreWithCreateObject(uint flags, IntPtr punkCreateObject, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int GetPropertyStoreForKeys(IntPtr rgKeys, uint cKeys, uint flags, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int GetPropertyDescriptionList(ref PropertyKey keyType, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int Update(IntPtr pbc);
        [PreserveSig] int GetProperty(ref PropertyKey key, IntPtr ppropvar);
        [PreserveSig] int GetCLSID(ref PropertyKey key, out Guid pclsid);
        [PreserveSig] int GetFileTime(ref PropertyKey key, out long pft);
        [PreserveSig] int GetInt32(ref PropertyKey key, out int pi);
        [PreserveSig] int GetString(ref PropertyKey key, [MarshalAs(UnmanagedType.LPWStr)] out string ppsz);
        [PreserveSig] int GetUInt32(ref PropertyKey key, out uint pui);
        [PreserveSig] int GetUInt64(ref PropertyKey key, out ulong pull);
        [PreserveSig] int GetBool(ref PropertyKey key, out int pf);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey(Guid fmtid, uint pid)
    {
        public Guid fmtid = fmtid;
        public uint pid = pid;
    }

    /// <summary>Well-known PROPERTYKEY values (fmtid + pid) used by the Details tab.</summary>
    private static class PropertyKeys
    {
        public static readonly PropertyKey Title = new(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), 2);
        public static readonly PropertyKey Subject = new(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), 3);
        public static readonly PropertyKey Author = new(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), 4);
        public static readonly PropertyKey Keywords = new(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), 5);
        public static readonly PropertyKey Comment = new(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), 6);
        public static readonly PropertyKey Copyright = new(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), 11);

        public static readonly PropertyKey Dimensions = new(new Guid("6444048F-4C8B-11D1-8B70-080036B11A03"), 13);
        public static readonly PropertyKey ImageWidth = new(new Guid("6444048F-4C8B-11D1-8B70-080036B11A03"), 3);
        public static readonly PropertyKey ImageHeight = new(new Guid("6444048F-4C8B-11D1-8B70-080036B11A03"), 4);
        public static readonly PropertyKey BitDepth = new(new Guid("6444048F-4C8B-11D1-8B70-080036B11A03"), 7);

        public static readonly PropertyKey DateTaken = new(new Guid("14B81DA1-0135-4D31-96D9-6CBFC9671A99"), 36867);
        public static readonly PropertyKey CameraManufacturer = new(new Guid("14B81DA1-0135-4D31-96D9-6CBFC9671A99"), 271);
        public static readonly PropertyKey CameraModel = new(new Guid("14B81DA1-0135-4D31-96D9-6CBFC9671A99"), 272);

        public static readonly PropertyKey Duration = new(new Guid("64440490-4C8B-11D1-8B70-080036B11A03"), 3);
        public static readonly PropertyKey TotalBitrate = new(new Guid("64440490-4C8B-11D1-8B70-080036B11A03"), 4);
        public static readonly PropertyKey FrameWidth = new(new Guid("64440491-4C8B-11D1-8B70-080036B11A03"), 3);
        public static readonly PropertyKey FrameHeight = new(new Guid("64440491-4C8B-11D1-8B70-080036B11A03"), 4);

        public static readonly PropertyKey MusicArtist = new(new Guid("56A3372E-CE9C-11D2-9F0E-006097C686F6"), 2);
        public static readonly PropertyKey MusicAlbum = new(new Guid("56A3372E-CE9C-11D2-9F0E-006097C686F6"), 4);
        public static readonly PropertyKey MusicGenre = new(new Guid("56A3372E-CE9C-11D2-9F0E-006097C686F6"), 11);

        public static readonly PropertyKey FileVersion = new(new Guid("0CEF7D53-FA64-11D1-A203-0000F81FEDEE"), 4);
        public static readonly PropertyKey ProductName = new(new Guid("0CEF7D53-FA64-11D1-A203-0000F81FEDEE"), 7);
        public static readonly PropertyKey ProductVersion = new(new Guid("0CEF7D53-FA64-11D1-A203-0000F81FEDEE"), 8);
    }
}
