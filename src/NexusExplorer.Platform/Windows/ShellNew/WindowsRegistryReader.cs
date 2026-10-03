using System.Runtime.Versioning;
using Microsoft.Win32;

namespace NexusExplorer.Platform.Windows.ShellNew;

/// <summary>
/// The real <see cref="IRegistryReader"/>, backed by <see cref="Registry.ClassesRoot"/> (HKCR,
/// the merged HKLM+HKCU class view the shell itself uses). Every member swallows registry
/// exceptions and returns a neutral result, so the parser can treat the registry as a plain data
/// source without defensive code of its own.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsRegistryReader : IRegistryReader
{
    public IReadOnlyList<string> GetExtensionKeys()
    {
        try
        {
            using var root = Registry.ClassesRoot;
            return root.GetSubKeyNames()
                .Where(n => n.StartsWith('.'))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    public string? GetDefaultValue(string keyPath)
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(keyPath);
            return key?.GetValue(null) as string;
        }
        catch
        {
            return null;
        }
    }

    public string? GetStringValue(string keyPath, string valueName)
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(keyPath);
            return key?.GetValue(valueName) as string;
        }
        catch
        {
            return null;
        }
    }

    public byte[]? GetBinaryValue(string keyPath, string valueName)
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(keyPath);
            return key?.GetValue(valueName) as byte[];
        }
        catch
        {
            return null;
        }
    }

    public bool KeyExists(string keyPath)
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(keyPath);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<string> GetValueNames(string keyPath)
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(keyPath);
            return key?.GetValueNames() ?? [];
        }
        catch
        {
            return [];
        }
    }

    public IReadOnlyList<string> GetSubKeyNames(string keyPath)
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(keyPath);
            return key?.GetSubKeyNames() ?? [];
        }
        catch
        {
            return [];
        }
    }
}
