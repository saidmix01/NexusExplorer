using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NexusExplorer.Infrastructure.Logging;

namespace NexusExplorer.App;

/// <summary>
/// Startup instrumentation. Records elapsed-time markers into the application
/// log so the cold-start cost of each component can be measured for a GUI app
/// (no console output).
/// </summary>
internal static class StartupTiming
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static long _last;
    private static readonly ILogger Logger = NexusLog.Create("Startup");

    /// <summary>
    /// Records a milestone with the time elapsed since the previous marker.
    /// Startup markers are logged at Information level so they are always
    /// available for diagnosing slow launches, even in release builds.
    /// </summary>
    public static void Mark(string name)
    {
        var elapsed = Clock.ElapsedMilliseconds;
        var sinceLast = elapsed - _last;
        _last = elapsed;

        Logger.LogInformation("{Name} (+{SinceLast} ms, total {Total} ms)", name, sinceLast, elapsed);
    }

    /// <summary>
    /// Records the point at which the UI is ready and the total startup time.
    /// </summary>
    public static void MarkReady(string name)
    {
        var total = Clock.ElapsedMilliseconds;
        _last = total;

        Logger.LogInformation("APPLICATION READY");
        Logger.LogInformation("{Name} in {Total} ms", name, total);
    }
}
