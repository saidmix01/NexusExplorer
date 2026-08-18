using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace NexusExplorer.App.Services.Thumbnails;

/// <summary>
/// Extracts native Windows shell icons/thumbnails for files using IShellItemImageFactory.
/// This produces high-quality icons at any requested size, unlike SHGetFileInfo which is limited to 32x32.
/// Falls back to SHGetFileInfo + SHGFI_JUMBO for older systems.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowsShellIconExtractor
{
    #region COM Interfaces

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [Flags]
    private enum SIIGBF
    {
        SIIGBF_RESIZETOFIT = 0x00000000,
        SIIGBF_BIGGERSIZEOK = 0x00000001,
        SIIGBF_MEMORYONLY = 0x00000002,
        SIIGBF_ICONONLY = 0x00000004,
        SIIGBF_THUMBNAILONLY = 0x00000008,
        SIIGBF_INCACHEONLY = 0x00000010,
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        out IShellItemImageFactory ppv);

    #endregion

    #region GDI / Bitmap conversion

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hObject, int nCount, ref BITMAP lpObject);

    [DllImport("gdi32.dll")]
    private static extern int GetBitmapBits(IntPtr hbmp, int cbBuffer, byte[] lpvBits);

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    #endregion

    #region Fallback: SHGetFileInfo

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHIL_JUMBO = 0x4;
    private const uint SHIL_EXTRALARGE = 0x2;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageList(int iImageList, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IImageList ppv);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    private interface IImageList
    {
        [PreserveSig]
        int Add(IntPtr hbmImage, IntPtr hbmMask, out int pi);
        [PreserveSig]
        int ReplaceIcon(int i, IntPtr hicon, out int pi);
        [PreserveSig]
        int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig]
        int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig]
        int AddMasked(IntPtr hbmImage, int crMask, out int pi);
        [PreserveSig]
        int Draw(IntPtr pimldp);
        [PreserveSig]
        int Remove(int i);
        [PreserveSig]
        int GetIcon(int i, int flags, out IntPtr picon);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    #endregion

    /// <summary>
    /// Returns true if we should attempt shell icon extraction for this file.
    /// </summary>
    public static bool ShouldExtractShellIcon(string? extension)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        var ext = extension?.ToLowerInvariant();
        return ext is ".lnk" or ".url" or ".exe" or ".msi" or ".appref-ms";
    }

    /// <summary>
    /// Extracts the shell icon/thumbnail for the given file path as an Avalonia Bitmap
    /// at the requested size. Uses IShellItemImageFactory for high quality.
    /// </summary>
    public static Task<Bitmap?> ExtractIconAsync(string filePath, int targetSize, CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Task.FromResult<Bitmap?>(null);

        return Task.Run(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Primary method: IShellItemImageFactory (high quality, any size)
                var bitmap = ExtractViaShellItemImageFactory(filePath, targetSize);
                if (bitmap != null)
                    return bitmap;

                // Fallback: SHGetImageList with jumbo/extralarge icons
                bitmap = ExtractViaImageList(filePath, targetSize);
                if (bitmap != null)
                    return bitmap;

                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Uses IShellItemImageFactory.GetImage to get a high-quality bitmap at the exact requested size.
    /// </summary>
    private static Bitmap? ExtractViaShellItemImageFactory(string filePath, int targetSize)
    {
        try
        {
            var riid = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(filePath, IntPtr.Zero, riid, out var factory);

            var size = new SIZE { cx = targetSize, cy = targetSize };
            var hr = factory.GetImage(size, SIIGBF.SIIGBF_RESIZETOFIT | SIIGBF.SIIGBF_ICONONLY, out var hBitmap);

            if (hr != 0 || hBitmap == IntPtr.Zero)
                return null;

            try
            {
                return HBitmapToAvaloniaBitmap(hBitmap);
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Fallback: Uses SHGetImageList to get jumbo (256px) or extra-large (48px) icons.
    /// </summary>
    private static Bitmap? ExtractViaImageList(string filePath, int targetSize)
    {
        try
        {
            var shfi = new SHFILEINFO();
            var result = SHGetFileInfo(filePath, 0, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_LARGEICON);

            if (result == IntPtr.Zero)
                return null;

            // We just needed the iIcon index, destroy the 32px icon
            if (shfi.hIcon != IntPtr.Zero)
                DestroyIcon(shfi.hIcon);

            // Try jumbo (256px) first, then extralarge (48px)
            var imageListSize = targetSize > 48 ? (int)SHIL_JUMBO : (int)SHIL_EXTRALARGE;
            var iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

            if (SHGetImageList(imageListSize, iidImageList, out var imageList) != 0)
                return null;

            if (imageList.GetIcon(shfi.iIcon, 0x00000001 /* ILD_TRANSPARENT */, out var hIcon) != 0 || hIcon == IntPtr.Zero)
                return null;

            try
            {
                return HIconToAvaloniaBitmap(hIcon);
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts an HBITMAP (from IShellItemImageFactory) to an Avalonia Bitmap.
    /// These HBITMAPs are already 32-bit BGRA with premultiplied alpha.
    /// </summary>
    private static Bitmap? HBitmapToAvaloniaBitmap(IntPtr hBitmap)
    {
        var bmp = new BITMAP();
        if (GetObject(hBitmap, Marshal.SizeOf<BITMAP>(), ref bmp) == 0)
            return null;

        var width = bmp.bmWidth;
        var height = bmp.bmHeight;

        if (width <= 0 || height <= 0 || bmp.bmBitsPixel != 32)
            return null;

        var stride = bmp.bmWidthBytes;
        var pixelDataSize = stride * height;
        var pixels = new byte[pixelDataSize];

        if (GetBitmapBits(hBitmap, pixelDataSize, pixels) == 0)
            return null;

        // Fix premultiplied alpha → straight alpha and check for valid alpha
        bool hasAlpha = false;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            var a = pixels[i + 3];
            if (a != 0) hasAlpha = true;
            if (a > 0 && a < 255)
            {
                // Un-premultiply
                pixels[i + 0] = (byte)Math.Min(255, pixels[i + 0] * 255 / a); // B
                pixels[i + 1] = (byte)Math.Min(255, pixels[i + 1] * 255 / a); // G
                pixels[i + 2] = (byte)Math.Min(255, pixels[i + 2] * 255 / a); // R
            }
        }

        if (!hasAlpha)
        {
            for (int i = 3; i < pixels.Length; i += 4)
                pixels[i] = 255;
        }

        return CreateBitmapFromBgra(width, height, pixels);
    }

    /// <summary>
    /// Converts an HICON to an Avalonia Bitmap using GetDIBits for proper alpha handling.
    /// </summary>
    private static Bitmap? HIconToAvaloniaBitmap(IntPtr hIcon)
    {
        if (GetIconInfo(hIcon, out var iconInfo) == 0)
            return null;

        try
        {
            if (iconInfo.hbmColor == IntPtr.Zero)
                return null;

            return HBitmapToAvaloniaBitmap(iconInfo.hbmColor);
        }
        finally
        {
            if (iconInfo.hbmColor != IntPtr.Zero) DeleteObject(iconInfo.hbmColor);
            if (iconInfo.hbmMask != IntPtr.Zero) DeleteObject(iconInfo.hbmMask);
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    /// <summary>
    /// Creates an Avalonia Bitmap from raw BGRA pixel data by writing a BMP with alpha.
    /// </summary>
    private static Bitmap? CreateBitmapFromBgra(int width, int height, byte[] bgraPixels)
    {
        try
        {
            using var ms = new MemoryStream();
            WriteBmpV4(ms, width, height, bgraPixels);
            ms.Position = 0;
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Writes a BITMAPV4 BMP file with proper alpha channel support.
    /// </summary>
    private static void WriteBmpV4(Stream stream, int width, int height, byte[] bgraPixels)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.Default, leaveOpen: true);

        const int headerSize = 14;
        const int dibHeaderSize = 108; // BITMAPV4HEADER
        var rowBytes = width * 4;
        var pixelDataSize = rowBytes * height;
        var fileSize = headerSize + dibHeaderSize + pixelDataSize;

        // BMP File Header (14 bytes)
        writer.Write((ushort)0x4D42); // 'BM'
        writer.Write(fileSize);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(headerSize + dibHeaderSize); // offset to pixel data

        // BITMAPV4HEADER (108 bytes)
        writer.Write(dibHeaderSize);
        writer.Write(width);
        writer.Write(-height); // top-down
        writer.Write((ushort)1); // planes
        writer.Write((ushort)32); // bpp
        writer.Write(3); // BI_BITFIELDS
        writer.Write(pixelDataSize);
        writer.Write(2835); // X ppm
        writer.Write(2835); // Y ppm
        writer.Write(0); // colors used
        writer.Write(0); // colors important

        // Channel masks (BGRA in memory → R, G, B, A masks)
        writer.Write(0x00FF0000); // R
        writer.Write(0x0000FF00); // G
        writer.Write(0x000000FF); // B
        writer.Write(unchecked((int)0xFF000000)); // A

        // LCS_sRGB
        writer.Write(0x73524742);
        writer.Write(new byte[36]); // CIEXYZTRIPLE
        writer.Write(0); // red gamma
        writer.Write(0); // green gamma
        writer.Write(0); // blue gamma

        // Pixel data
        writer.Write(bgraPixels);
    }
}
