using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace Highbyte.DotNet6502.Systems.Logging.InMem;

public static class DotNet6502InMemLoggingBuilderExtensions
{
    public static ILoggingBuilder AddInMem(this ILoggingBuilder builder, DotNet6502InMemLogStore logStore)
    {
        return builder.AddInMem(new DotNet6502InMemLoggerConfiguration(logStore));
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Logger provider options are application-owned configuration types and remain rooted by logging registration.")]
    public static ILoggingBuilder AddInMem(this ILoggingBuilder builder, DotNet6502InMemLoggerConfiguration config)
    {
        builder.AddConfiguration();

        builder.Services.AddSingleton(config);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, DotNet6502InMemLoggerProvider>());

        LoggerProviderOptions.RegisterProviderOptions
            <DotNet6502InMemLoggerConfiguration, DotNet6502InMemLoggerProvider>(builder.Services);

        return builder;
    }
}
