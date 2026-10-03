using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NexusExplorer.Core.Abstractions;
using NexusExplorer.Core.Models;

namespace NexusExplorer.Infrastructure.Persistence;

/// <summary>
/// Persists application state to a JSON file in the local app data directory.
/// </summary>
public sealed class StatePersistenceService : IStatePersistenceService
{
    private readonly ILogger<StatePersistenceService> _logger;
    private readonly string _settingsPath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public StatePersistenceService(ILogger<StatePersistenceService> logger)
    {
        _logger = logger;
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NexusExplorer");
        Directory.CreateDirectory(appDataDir);
        _settingsPath = Path.Combine(appDataDir, "session-state.json");
    }

    public async Task SaveAsync(AppState state, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(state, JsonOptions);

            // Write atomically: serialize to a temp file first, then replace the target.
            // This prevents a truncated/corrupt session-state.json if the process is
            // interrupted mid-write (which is common — state is saved during shutdown).
            var tempPath = _settingsPath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);

            if (File.Exists(_settingsPath))
                File.Replace(tempPath, _settingsPath, null);
            else
                File.Move(tempPath, _settingsPath);

            _logger.LogDebug("State saved to {Path}", _settingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save application state to {Path}", _settingsPath);
        }
    }

    public async Task<AppState?> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return null;

            var json = await File.ReadAllTextAsync(_settingsPath, cancellationToken);
            var state = JsonSerializer.Deserialize<AppState>(json, JsonOptions);
            _logger.LogDebug("State loaded from {Path}", _settingsPath);
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load application state from {Path}", _settingsPath);
            return null;
        }
    }
}
