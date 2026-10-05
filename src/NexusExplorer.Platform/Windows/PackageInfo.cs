using System.Runtime.InteropServices;

namespace NexusExplorer.Platform.Windows;

/// <summary>
/// Tells whether the process is running with MSIX package identity (installed from the
/// Microsoft Store or via an .msix), so features that don't work under MSIX can be hidden.
/// </summary>
public static class PackageInfo
{
    private const int AppModelErrorNoPackage = 15700;
    private const int ErrorInsufficientBuffer = 122;

    private static readonly Lazy<bool> _isPackaged = new(Detect);

    /// <summary>True when running from an MSIX package.</summary>
    public static bool IsPackaged => _isPackaged.Value;

    private static bool Detect()
    {
        // GetCurrentPackageFullName exists from Windows 8; older systems can't be packaged anyway.
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 2)) return false;

        try
        {
            var length = 0;
            var result = GetCurrentPackageFullName(ref length, null);
            // An unpackaged process gets APPMODEL_ERROR_NO_PACKAGE; a packaged one is asked
            // for a bigger buffer to receive its package full name.
            return result == ErrorInsufficientBuffer;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);
}
