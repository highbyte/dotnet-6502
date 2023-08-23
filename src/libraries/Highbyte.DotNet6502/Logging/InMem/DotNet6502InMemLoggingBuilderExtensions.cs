using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;

namespace Highbyte.DotNet6502.Logging.InMem;

public static class DotNet6502InMemLoggingBuilderExtensions
{
    public static ILoggingBuilder AddInMem(this ILoggingBuilder builder, DotNet6502InMemLogStore logStore)
    {
        return AddInMem(builder, new DotNet6502InMemLoggerConfiguration(logStore));
    }

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
