using System.Globalization;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// ViewModel for the Properties dialog. Supports single file, single folder, and multi-selection.
/// </summary>
public partial class PropertiesViewModel : ObservableObject
{
    private readonly IFilePropertiesService _propertiesService;
    private readonly IReadOnlyList<FileSystemItem> _items;
    private CancellationTokenSource? _sizeCts;

    // --- Display properties ---

    [ObservableProperty]
    private string _title = "Properties";

    /// <summary>Selected section in the properties window: 0=General, 1=Details, 2=Security.</summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _itemName = "";

    [ObservableProperty]
    private string _typeDescription = "";

    [ObservableProperty]
    private string _location = "";

    [ObservableProperty]
    private string _sizeText = "";

    [ObservableProperty]
    private string _containsText = "";

    [ObservableProperty]
    private string _createdText = "";

    [ObservableProperty]
    private string _modifiedText = "";

    [ObservableProperty]
    private string _accessedText = "";

    [ObservableProperty]
    private bool _isReadOnly;

    [ObservableProperty]
    private bool _isHidden;

    [ObservableProperty]
    private bool _isSingleItem;

    [ObservableProperty]
    private bool _isDirectory;

    [ObservableProperty]
    private bool _isMultipleItems;

    [ObservableProperty]
    private bool _isCalculatingSize;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _attributesChanged;

    // --- General tab (extra) ---
    [ObservableProperty]
    private string _sizeOnDiskText = "";

    [ObservableProperty]
    private string _ownerText = "";

    [ObservableProperty]
    private string _opensWithText = "";

    [ObservableProperty]
    private bool _isSystem;

    // --- Details tab ---
    public ObservableCollection<DetailGroupViewModel> DetailGroups { get; } = [];

    [ObservableProperty]
    private bool _isLoadingDetails;

    // --- Security tab ---
    public ObservableCollection<SecurityEntry> SecurityEntries { get; } = [];

    [ObservableProperty]
    private string _securityOwnerText = "";

    [ObservableProperty]
    private string? _securityNote;

    [ObservableProperty]
    private bool _isLoadingSecurity;

    /// <summary>
    /// Fired when attributes have been applied and the Explorer should refresh.
    /// </summary>
    public event Action? AttributesApplied;

    /// <summary>
    /// Fired to request closing the dialog.
    /// </summary>
    public event Action? CloseRequested;

    public PropertiesViewModel(IFilePropertiesService propertiesService, IReadOnlyList<FileSystemItem> items)
    {
        _propertiesService = propertiesService;
        _items = items;

        if (items.Count == 1)
        {
            IsSingleItem = true;
            IsMultipleItems = false;
        }
        else
        {
            IsSingleItem = false;
            IsMultipleItems = true;
        }
    }

    /// <summary>
    /// Loads properties asynchronously. Call after the dialog is shown.
    /// </summary>
    public async Task LoadAsync()
    {
        if (_items.Count == 0) return;

        _sizeCts?.Cancel();
        _sizeCts = new CancellationTokenSource();
        var ct = _sizeCts.Token;

        try
        {
            if (_items.Count == 1)
                await LoadSingleItemAsync(_items[0], ct);
            else
                await LoadMultipleItemsAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Dialog closed during loading
        }
        catch (FileNotFoundException ex)
        {
            HasError = true;
            ErrorMessage = $"Not found: {ex.FileName}";
        }
        catch (UnauthorizedAccessException)
        {
            HasError = true;
            ErrorMessage = "Access denied.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Error: {ex.Message}";
        }
    }

    private async Task LoadSingleItemAsync(FileSystemItem item, CancellationToken ct)
    {
        var props = await _propertiesService.GetPropertiesAsync(item.Path, ct);

        ItemName = props.Name;
        Title = $"Properties - {props.Name}";
        TypeDescription = props.TypeDescription;
        Location = props.DirectoryPath;
        IsDirectory = props.IsDirectory;
        IsReadOnly = props.IsReadOnly;
        IsHidden = props.IsHidden;
        IsSystem = props.IsSystem;
        CreatedText = FormatDate(props.Created);
        ModifiedText = FormatDate(props.Modified);
        AccessedText = FormatDate(props.LastAccessed);
        OwnerText = props.Owner ?? "";
        OpensWithText = props.OpensWith ?? "";

        if (props.IsDirectory)
        {
            SizeText = "Calculating...";
            ContainsText = "Calculating...";
            IsCalculatingSize = true;

            var progress = new Progress<DirectorySizeProgress>(p =>
            {
                SizeText = FormatSize(p.CurrentSize);
                ContainsText = $"{p.FileCount:N0} files, {p.FolderCount:N0} folders";
            });

            try
            {
                var result = await _propertiesService.CalculateDirectorySizeAsync(item.Path, progress, ct);
                SizeText = FormatSize(result.TotalSize);
                SizeOnDiskText = FormatSize(result.TotalSize);
                ContainsText = $"{result.FileCount:N0} files, {result.FolderCount:N0} folders";
            }
            catch (OperationCanceledException) { }
            finally
            {
                IsCalculatingSize = false;
            }
        }
        else
        {
            SizeText = FormatSize(props.Size);
            SizeOnDiskText = FormatSize(props.SizeOnDisk);
            ContainsText = "";
        }

        // Load the Details and Security tabs in the background (non-blocking for General).
        _ = LoadDetailsAsync(item.Path, ct);
        _ = LoadSecurityAsync(item.Path, ct);
    }

    private async Task LoadDetailsAsync(string path, CancellationToken ct)
    {
        IsLoadingDetails = true;
        try
        {
            var groups = await _propertiesService.GetDetailsAsync(path, ct);
            ct.ThrowIfCancellationRequested();

            DetailGroups.Clear();
            foreach (var g in groups)
                DetailGroups.Add(new DetailGroupViewModel(g.Name, g.Properties));
        }
        catch (OperationCanceledException) { }
        catch { /* details are best-effort */ }
        finally
        {
            IsLoadingDetails = false;
        }
    }

    private async Task LoadSecurityAsync(string path, CancellationToken ct)
    {
        IsLoadingSecurity = true;
        try
        {
            var info = await _propertiesService.GetSecurityAsync(path, ct);
            ct.ThrowIfCancellationRequested();

            SecurityOwnerText = info.Owner ?? "";
            SecurityNote = info.Note;
            SecurityEntries.Clear();
            foreach (var e in info.Entries)
                SecurityEntries.Add(e);
        }
        catch (OperationCanceledException) { }
        catch { /* security is best-effort */ }
        finally
        {
            IsLoadingSecurity = false;
        }
    }

    private async Task LoadMultipleItemsAsync(CancellationToken ct)
    {
        var fileCount = _items.Count(i => i.Type == FileSystemItemType.File);
        var folderCount = _items.Count(i => i.Type == FileSystemItemType.Directory);

        ItemName = $"{_items.Count} items selected";
        Title = $"Properties - {_items.Count} items";
        TypeDescription = "Multiple items";
        Location = Path.GetDirectoryName(_items[0].Path) ?? "";
        IsDirectory = false;
        CreatedText = "";
        ModifiedText = "";
        AccessedText = "";

        ContainsText = FormatItemCounts(fileCount, folderCount);

        // Calculate total size
        SizeText = "Calculating...";
        IsCalculatingSize = true;

        long totalSize = 0;
        int totalFiles = 0;
        int totalFolders = 0;

        try
        {
            foreach (var item in _items)
            {
                ct.ThrowIfCancellationRequested();

                if (item.Type == FileSystemItemType.File)
                {
                    totalSize += item.Size ?? 0;
                    totalFiles++;
                }
                else
                {
                    var progress = new Progress<DirectorySizeProgress>(p =>
                    {
                        SizeText = FormatSize(totalSize + p.CurrentSize);
                    });

                    var result = await _propertiesService.CalculateDirectorySizeAsync(item.Path, progress, ct);
                    totalSize += result.TotalSize;
                    totalFiles += result.FileCount;
                    totalFolders += result.FolderCount + 1; // +1 for the folder itself
                }
            }

            SizeText = FormatSize(totalSize);
            ContainsText = FormatItemCounts(fileCount, folderCount) + $" ({totalFiles:N0} files, {totalFolders:N0} folders total)";
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsCalculatingSize = false;
        }

        // For multiple items, show combined attributes
        IsReadOnly = _items.All(i => i.IsReadOnly);
        IsHidden = _items.All(i => i.IsHidden);
    }

    [RelayCommand]
    private async Task ApplyAttributesAsync()
    {
        try
        {
            foreach (var item in _items)
            {
                await _propertiesService.SetAttributesAsync(item.Path, IsReadOnly, IsHidden);
            }
            AttributesChanged = false;
            AttributesApplied?.Invoke();
        }
        catch (UnauthorizedAccessException)
        {
            HasError = true;
            ErrorMessage = "Access denied. Cannot change attributes.";
        }
        catch (IOException ex)
        {
            HasError = true;
            ErrorMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Close()
    {
        CancelCalculation();
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void CopyPath()
    {
        if (_items.Count == 1)
        {
            var path = _items[0].Path;
            CopyPathRequested?.Invoke(path);
        }
    }

    [RelayCommand]
    private void OpenLocation()
    {
        if (_items.Count == 1)
        {
            OpenLocationRequested?.Invoke(_items[0].Path);
        }
    }

    /// <summary>
    /// Fired when Copy Path is requested — the View sets this to the system clipboard.
    /// </summary>
    public event Action<string>? CopyPathRequested;

    /// <summary>
    /// Fired when Open Location is requested — navigates to the item in Nexus.
    /// </summary>
    public event Action<string>? OpenLocationRequested;

    /// <summary>
    /// Fired when navigation to a path is requested (from Open Location).
    /// The MainWindowViewModel subscribes to this.
    /// </summary>
    public event Action<string>? NavigationRequested;

    public void RequestNavigation(string path)
    {
        NavigationRequested?.Invoke(path);
    }

    partial void OnIsReadOnlyChanged(bool value) => AttributesChanged = true;
    partial void OnIsHiddenChanged(bool value) => AttributesChanged = true;

    public void CancelCalculation()
    {
        _sizeCts?.Cancel();
        _sizeCts?.Dispose();
        _sizeCts = null;
    }

    private static string FormatDate(DateTime? date)
    {
        if (date is null) return "";
        return date.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes == 0) return "0 bytes";

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var unitIndex = 0;
        var size = (double)bytes;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        var formatted = $"{size:0.##} {units[unitIndex]}";
        if (unitIndex > 0)
            formatted += $" ({bytes:N0} bytes)";

        return formatted;
    }

    private static string FormatItemCounts(int files, int folders)
    {
        var parts = new List<string>();
        if (files > 0) parts.Add($"{files} file{(files != 1 ? "s" : "")}");
        if (folders > 0) parts.Add($"{folders} folder{(folders != 1 ? "s" : "")}");
        return string.Join(", ", parts);
    }
}

/// <summary>Bindable wrapper for a detail group (a header + its key/value rows) in the Details tab.</summary>
public sealed class DetailGroupViewModel(string name, IReadOnlyList<DetailProperty> properties)
{
    public string Name { get; } = name;
    public IReadOnlyList<DetailProperty> Properties { get; } = properties;
}
