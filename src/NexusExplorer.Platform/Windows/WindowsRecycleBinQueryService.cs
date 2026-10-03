using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NexusExplorer.Core.Abstractions;

namespace NexusExplorer.Platform.Windows;

/// <summary>
/// Lists, restores and empties the Windows Recycle Bin using the late-bound
/// <c>Shell.Application</c> COM automation object (folder namespace 10 = ssfBITBUCKET).
/// Reflection is used for the COM calls so the project does not need a Windows-specific TFM
/// or a COM interop assembly.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsRecycleBinQueryService : IRecycleBinQueryService
{
    private const int SsfBitBucket = 10;

    // System.Recycle.DateDeleted / original-location shell property canonical names.
    private const string PropDateDeleted = "System.Recycle.DateDeleted";
    private const string PropOriginalLocation = "System.Recycle.DeletedFrom";

    public bool IsSupported => OperatingSystem.IsWindows();

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    public Task<IReadOnlyList<RecycleBinEntry>> EnumerateAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<RecycleBinEntry>>(() =>
        {
            var entries = new List<RecycleBinEntry>();
            if (!OperatingSystem.IsWindows()) return entries;

            object? shell = null;
            object? folder = null;
            try
            {
                var shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType is null) return entries;
                shell = Activator.CreateInstance(shellType);
                if (shell is null) return entries;

                folder = shellType.InvokeMember("NameSpace",
                    BindingFlags.InvokeMethod, null, shell, [SsfBitBucket]);
                if (folder is null) return entries;

                var folderType = folder.GetType();
                var items = folderType.InvokeMember("Items",
                    BindingFlags.InvokeMethod, null, folder, null);
                if (items is null) return entries;

                var itemsType = items.GetType();
                var count = (int)(itemsType.InvokeMember("Count",
                    BindingFlags.GetProperty, null, items, null) ?? 0);

                for (int i = 0; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var item = itemsType.InvokeMember("Item",
                        BindingFlags.InvokeMethod, null, items, [i]);
                    if (item is null) continue;
                    var itemType = item.GetType();

                    string name = GetString(itemType, item, "Name");
                    bool isFolder = GetBool(itemType, item, "IsFolder");
                    long? size = GetLong(itemType, item, "Size");

                    // Original location (folder) + name → original full path.
                    var originalDir = GetDetail(folderType, folder, item, PropOriginalLocation);
                    var originalPath = !string.IsNullOrEmpty(originalDir)
                        ? System.IO.Path.Combine(originalDir, name)
                        : GetString(itemType, item, "Path");

                    DateTime? deletedAt = null;
                    var deletedRaw = GetDetail(folderType, folder, item, PropDateDeleted);
                    if (!string.IsNullOrEmpty(deletedRaw) && DateTime.TryParse(deletedRaw, out var dt))
                        deletedAt = dt;

                    entries.Add(new RecycleBinEntry
                    {
                        Name = name,
                        OriginalPath = originalPath,
                        IsDirectory = isFolder,
                        Size = size,
                        DeletedAt = deletedAt
                    });
                }
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // Shell automation can fail transiently; return whatever we gathered.
            }
            finally
            {
                Release(folder);
                Release(shell);
            }

            return entries;
        }, cancellationToken);
    }

    public Task<bool> RestoreAsync(string originalPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(originalPath)) return false;

            object? shell = null;
            object? folder = null;
            try
            {
                var shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType is null) return false;
                shell = Activator.CreateInstance(shellType);
                if (shell is null) return false;

                folder = shellType.InvokeMember("NameSpace",
                    BindingFlags.InvokeMethod, null, shell, [SsfBitBucket]);
                if (folder is null) return false;

                var folderType = folder.GetType();
                var items = folderType.InvokeMember("Items",
                    BindingFlags.InvokeMethod, null, folder, null);
                if (items is null) return false;

                var itemsType = items.GetType();
                var count = (int)(itemsType.InvokeMember("Count",
                    BindingFlags.GetProperty, null, items, null) ?? 0);

                for (int i = 0; i < count; i++)
                {
                    var item = itemsType.InvokeMember("Item",
                        BindingFlags.InvokeMethod, null, items, [i]);
                    if (item is null) continue;
                    var itemType = item.GetType();

                    var name = GetString(itemType, item, "Name");
                    var originalDir = GetDetail(folderType, folder, item, PropOriginalLocation);
                    var fullPath = !string.IsNullOrEmpty(originalDir)
                        ? System.IO.Path.Combine(originalDir, name)
                        : GetString(itemType, item, "Path");

                    if (!string.Equals(fullPath, originalPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (InvokeRestoreVerb(itemType, item))
                        return true;
                }
            }
            catch
            {
                // ignore
            }
            finally
            {
                Release(folder);
                Release(shell);
            }
            return false;
        }, cancellationToken);
    }

    public Task<bool> EmptyAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (!OperatingSystem.IsWindows()) return false;
            try
            {
                var hr = SHEmptyRecycleBin(IntPtr.Zero, null,
                    SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                return hr == 0;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }

    // --- reflection helpers ---

    private static bool InvokeRestoreVerb(Type itemType, object item)
    {
        var verbs = itemType.InvokeMember("Verbs", BindingFlags.InvokeMethod, null, item, null);
        if (verbs is null) return false;
        var verbsType = verbs.GetType();
        var count = (int)(verbsType.InvokeMember("Count", BindingFlags.GetProperty, null, verbs, null) ?? 0);

        for (int v = 0; v < count; v++)
        {
            var verb = verbsType.InvokeMember("Item", BindingFlags.InvokeMethod, null, verbs, [v]);
            if (verb is null) continue;
            var name = GetString(verb.GetType(), verb, "Name");
            // Match "Restore" across common localizations by stripping accelerator '&'.
            var clean = name.Replace("&", "");
            if (clean.Contains("Restore", StringComparison.OrdinalIgnoreCase)
                || clean.Contains("Restaur", StringComparison.OrdinalIgnoreCase)
                || clean.Contains("Rétabl", StringComparison.OrdinalIgnoreCase))
            {
                verb.GetType().InvokeMember("DoIt", BindingFlags.InvokeMethod, null, verb, null);
                Release(verb);
                return true;
            }
            Release(verb);
        }
        return false;
    }

    private static string GetDetail(Type folderType, object folder, object item, string canonicalProperty)
    {
        // Folder.GetDetailsOf with a canonical property name is not directly available via the
        // automation object; instead use FolderItem.ExtendedProperty(canonicalName).
        try
        {
            var val = item.GetType().InvokeMember("ExtendedProperty",
                BindingFlags.InvokeMethod, null, item, [canonicalProperty]);
            return val?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetString(Type t, object o, string prop)
    {
        try { return t.InvokeMember(prop, BindingFlags.GetProperty, null, o, null)?.ToString() ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static bool GetBool(Type t, object o, string prop)
    {
        try { return t.InvokeMember(prop, BindingFlags.GetProperty, null, o, null) is true; }
        catch { return false; }
    }

    private static long? GetLong(Type t, object o, string prop)
    {
        try
        {
            var val = t.InvokeMember(prop, BindingFlags.GetProperty, null, o, null);
            if (val is null) return null;
            return Convert.ToInt64(val);
        }
        catch { return null; }
    }

    private static void Release(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
            Marshal.ReleaseComObject(comObject);
    }
}
