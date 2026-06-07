using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace Highbyte.DotNet6502.Systems.Logging.Console;

public static class DotNet6502ConsoleLoggingBuilderExtensions
{
    public static ILoggingBuilder AddDotNet6502Console(this ILoggingBuilder builder)
    {
        return builder.AddDotNet6502Console(new DotNet6502ConsoleLoggerConfiguration());
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Logger provider options are application-owned configuration types and remain rooted by logging registration.")]
    public static ILoggingBuilder AddDotNet6502Console(this ILoggingBuilder builder, DotNet6502ConsoleLoggerConfiguration config)
    {
        builder.AddConfiguration();

        builder.Services.AddSingleton(config);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, DotNet6502ConsoleLoggerProvider>());

        LoggerProviderOptions.RegisterProviderOptions
            <DotNet6502ConsoleLoggerConfiguration, DotNet6502ConsoleLoggerProvider>(builder.Services);

        return builder;
    }
}
