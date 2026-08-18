using Microsoft.Extensions.DependencyInjection;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Services;
using NexusExplorer.Infrastructure.FileOperations;
using NexusExplorer.Infrastructure.FileSystem;
using NexusExplorer.Infrastructure.Logging;
using NexusExplorer.Infrastructure.Persistence;
using NexusExplorer.Infrastructure.Preview;
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
        services.AddSingleton<IFilePropertiesService, FilePropertiesService>();
        services.AddSingleton<IOperationHistoryService, OperationHistoryService>();
        services.AddSingleton<IStatePersistenceService, StatePersistenceService>();
        services.AddSingleton<IFolderColorService, NexusExplorer.Infrastructure.Services.FolderColorService>();

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
