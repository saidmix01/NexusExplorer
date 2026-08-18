using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NexusExplorer.Infrastructure.Logging;

/// <summary>
/// Registers the shared file logging infrastructure so services can resolve
/// <see cref="ILogger{T}"/> via constructor injection.
/// </summary>
public static class LoggingServiceCollectionExtensions
{
    public static IServiceCollection AddNexusFileLogging(this IServiceCollection services)
    {
        services.AddSingleton(NexusLog.Options);
        services.AddSingleton<ILoggerFactory>(NexusLog.Factory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        return services;
    }
}
