using Microsoft.Extensions.Logging;

namespace NexusExplorer.Infrastructure.Logging;

/// <summary>
/// Process-wide logging bootstrap. Initializes a single file logger provider and
/// logger factory so startup instrumentation (which runs before the DI container
/// exists) and DI-resolved services share the same log infrastructure and files.
/// </summary>
public static class NexusLog
{
    private static readonly object Gate = new();
    private static FileLoggerOptions? _options;
    private static FileLoggerProvider? _provider;
    private static ILoggerFactory? _factory;

    public static FileLoggerOptions Options
    {
        get
        {
            EnsureInitialized();
            return _options!;
        }
    }

    public static ILoggerFactory Factory
    {
        get
        {
            EnsureInitialized();
            return _factory!;
        }
    }

    public static FileLoggerProvider Provider
    {
        get
        {
            EnsureInitialized();
            return _provider!;
        }
    }

    public static ILogger Create(string category) => Factory.CreateLogger(category);

    private static void EnsureInitialized()
    {
        lock (Gate)
        {
            if (_factory is not null)
                return;

            _options = new FileLoggerOptions();
#if DEBUG
            _options.MinimumLevel = LogLevel.Debug;
#endif
            _provider = new FileLoggerProvider(_options);
            _factory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(_options.MinimumLevel);
                builder.AddProvider(_provider);
            });
        }
    }
}
