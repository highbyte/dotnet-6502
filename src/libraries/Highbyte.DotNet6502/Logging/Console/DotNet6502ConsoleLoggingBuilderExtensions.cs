using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;

namespace Highbyte.DotNet6502.Logging.Console;

public static class DotNet6502ConsoleLoggingBuilderExtensions
{
    public static ILoggingBuilder AddDotNet6502Console(this ILoggingBuilder builder)
    {
        return AddDotNet6502Console(builder, new DotNet6502ConsoleLoggerConfiguration());
    }

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
