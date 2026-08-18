using Microsoft.Extensions.Logging;

namespace NexusExplorer.Infrastructure.Logging;

/// <summary>
/// Configuration for the file logging provider.
/// </summary>
public sealed class FileLoggerOptions
{
    /// <summary>
    /// Directory where log files are written. Defaults to the user's local
    /// application-data folder, which is the correct writable location for a
    /// desktop application on Windows, Linux and macOS.
    /// </summary>
    public string LogDirectory { get; set; } = GetDefaultLogDirectory();

    /// <summary>
    /// File name prefix. Daily files are named "{prefix}-yyyy-MM-dd.log".
    /// </summary>
    public string FileNamePrefix { get; set; } = "nexus";

    /// <summary>
    /// Minimum level written to disk. Debug builds log from Debug; release builds
    /// log from Information by default (see <see cref="NexusLog"/>).
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Maximum size of a single log file before it rolls to a new file.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Number of days of logs to keep before older files are deleted.
    /// </summary>
    public int RetainedDays { get; set; } = 14;

    /// <summary>
    /// Resolves the default log directory for the current operating system.
    /// </summary>
    public static string GetDefaultLogDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "NexusExplorer", "Logs");
    }
}
