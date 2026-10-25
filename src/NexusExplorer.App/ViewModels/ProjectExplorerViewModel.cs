using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models.Projects;

namespace NexusExplorer.App.ViewModels;

/// <summary>
/// View model for the Project Explorer: an alternative, logical representation of a folder that
/// Nexus recognizes as a software project. It only presents data from <see cref="ProjectInfo"/>
/// and delegates "open" actions back to the host (the normal file explorer navigation) — it holds
/// no detection or file-system logic and never executes project commands.
/// </summary>
public sealed partial class ProjectExplorerViewModel : ObservableObject
{
    private readonly IProjectDetectionService _detectionService;
    private readonly ILogger<ProjectExplorerViewModel> _logger;

    // Supplied by the host so entry activation reuses the existing navigation/open pipeline
    // and the existing terminal-run pipeline instead of duplicating them here.
    private readonly Func<string, Task> _openFolderAsync;
    private readonly Func<string, Task> _openFileAsync;
    private readonly Func<ProjectCommand, Task> _runCommandAsync;

    private CancellationTokenSource? _detectCts;

    [ObservableProperty]
    private ProjectInfo? _project;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>The sections to render; empty when no project is detected.</summary>
    public ObservableCollection<ProjectSection> Sections { get; } = [];

    /// <summary>True when a project was detected for the current folder.</summary>
    public bool HasProject => Project is not null;

    public ProjectExplorerViewModel(
        IProjectDetectionService detectionService,
        Func<string, Task> openFolderAsync,
        Func<string, Task> openFileAsync,
        Func<ProjectCommand, Task> runCommandAsync,
        ILogger<ProjectExplorerViewModel>? logger = null)
    {
        _detectionService = detectionService;
        _openFolderAsync = openFolderAsync;
        _openFileAsync = openFileAsync;
        _runCommandAsync = runCommandAsync;
        _logger = logger ?? NullLogger<ProjectExplorerViewModel>.Instance;
    }

    /// <summary>Design-time constructor for the XAML previewer.</summary>
    public ProjectExplorerViewModel()
    {
        _detectionService = null!;
        _openFolderAsync = _ => Task.CompletedTask;
        _openFileAsync = _ => Task.CompletedTask;
        _runCommandAsync = _ => Task.CompletedTask;
        _logger = NullLogger<ProjectExplorerViewModel>.Instance;
    }

    /// <summary>
    /// Runs detection for the given folder (off the UI thread) and refreshes the view.
    /// Cancels any in-flight detection so rapid navigation doesn't race.
    /// </summary>
    public async Task LoadAsync(string folderPath)
    {
        _detectCts?.Cancel();
        _detectCts?.Dispose();
        _detectCts = new CancellationTokenSource();
        var ct = _detectCts.Token;

        IsLoading = true;
        try
        {
            var info = _detectionService is null
                ? null
                : await _detectionService.DetectAsync(folderPath, ct).ConfigureAwait(true);

            if (ct.IsCancellationRequested)
                return;

            Project = info;
            Sections.Clear();
            if (info is not null)
            {
                foreach (var section in info.Sections)
                    Sections.Add(section);
            }
            OnPropertyChanged(nameof(HasProject));
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer navigation — ignore.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Project detection failed for {Path}.", folderPath);
            Project = null;
            Sections.Clear();
            OnPropertyChanged(nameof(HasProject));
        }
        finally
        {
            // Only the newest (non-cancelled) run owns the loading flag; a superseded run must
            // not clear it, but if this run was cancelled before producing results it also must
            // not leave the flag stuck — the superseding run will have already set it true again.
            if (ct == _detectCts?.Token)
                IsLoading = false;
        }
    }

    /// <summary>Clears the current project view (e.g. when leaving Project mode).</summary>
    public void Clear()
    {
        _detectCts?.Cancel();
        Project = null;
        Sections.Clear();
        IsLoading = false;
        OnPropertyChanged(nameof(HasProject));
    }

    /// <summary>
    /// Activates a logical entry: files/folders are mapped back to the real file system through
    /// the host's navigation pipeline, and command entries are run in the integrated terminal
    /// through the host's run pipeline. Information entries are inert.
    /// </summary>
    [RelayCommand]
    private async Task OpenEntryAsync(ProjectEntry? entry)
    {
        if (entry is null)
            return;

        switch (entry.Kind)
        {
            case ProjectEntryKind.Directory when entry.Path is { Length: > 0 } dir:
                await _openFolderAsync(dir).ConfigureAwait(false);
                break;
            case ProjectEntryKind.File when entry.Path is { Length: > 0 } file:
                await _openFileAsync(file).ConfigureAwait(false);
                break;
            case ProjectEntryKind.Command:
                await RunCommandAsync(entry.Command).ConfigureAwait(false);
                break;
            // Information entries have no action.
        }
    }

    /// <summary>
    /// Runs a project command (script or tooling command) in the integrated terminal via the
    /// host's run pipeline. Used by the Run button on command rows.
    /// </summary>
    [RelayCommand]
    private async Task RunCommandAsync(ProjectCommand? command)
    {
        if (command is null)
            return;
        await _runCommandAsync(command).ConfigureAwait(false);
    }

    partial void OnProjectChanged(ProjectInfo? value) => OnPropertyChanged(nameof(HasProject));
}
