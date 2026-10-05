using Microsoft.Extensions.DependencyInjection;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Services;
using NexusExplorer.Infrastructure.FileOperations;
using NexusExplorer.Infrastructure.FileSystem;
using NexusExplorer.Infrastructure.Logging;
using NexusExplorer.Infrastructure.Persistence;
using NexusExplorer.Infrastructure.Preview;
using NexusExplorer.Infrastructure.Projects;
using NexusExplorer.Infrastructure.Projects.Detectors;
using NexusExplorer.Platform;

namespace NexusExplorer.Infrastructure.DependencyInjection;

/// <summary>
/// Composition root: registers all application services.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNexusExplorer(this IServiceCollection services)
    {
        // Core services
        services.AddSingleton<ITabService, TabService>();

        // Infrastructure services
        services.AddSingleton<IFileWatcherService, FileWatcherService>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IPreviewService, PreviewService>();
        services.AddSingleton<IFileOperationService, FileOperationService>();
        services.AddSingleton<IFileOperationManager, FileOperationManager>();
        services.AddSingleton<ICompressionService, CompressionService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<ISearchService, SearchService>();
        // FilePropertiesService optionally consumes a platform IShellMetadataProvider (Windows).
        // Resolve it with GetService so the absence of a provider (Linux/macOS) is fine.
        services.AddSingleton<IFilePropertiesService>(sp =>
            new FilePropertiesService(sp.GetService<IShellMetadataProvider>()));
        services.AddSingleton<IOperationHistoryService, OperationHistoryService>();
        services.AddSingleton<IStatePersistenceService, StatePersistenceService>();
        services.AddSingleton<IFolderColorService, NexusExplorer.Infrastructure.Services.FolderColorService>();

        // Project detection (Developer Mode: shared by Project Explorer, Nexus Actions, …).
        // Add a new IProjectDetector here to support another ecosystem — no other code changes needed.
        services.AddSingleton<IProjectDetector, NodeProjectDetector>();
        services.AddSingleton<IProjectDetector, DotNetProjectDetector>();
        services.AddSingleton<IProjectDetector, RustProjectDetector>();
        services.AddSingleton<IProjectDetector, PythonProjectDetector>();
        services.AddSingleton<IProjectDetectionService, ProjectDetectionService>();

        // Installed editor discovery / launch
        services.AddSingleton<IEditorService, EditorService>();

        // External terminal launcher (cross-platform; discovery is registered per platform)
        services.AddSingleton<ITerminalLauncher, TerminalLauncher>();

        // Platform-specific services
        services.AddPlatformServices();

        // Logging (file-backed, shared with startup instrumentation)
        services.AddNexusFileLogging();

        return services;
    }
}
