using Highbyte.DotNet6502.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.ConsoleUI
{
    class Program
    {
        //static void Main(string[] args)
        static void Main()
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider(); 

            var run6502FunctionalTest = serviceProvider.GetService<Run6502FunctionalTest>();
            run6502FunctionalTest.Run();

            //RunTestProgram.Run();
            //RunTestProgram2.Run();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(configure => 
                configure.AddConsole()
            );

            var logLevel = LogLevel.Information;
            //var logLevel = LogLevel.Trace;
            services.Configure<LoggerFilterOptions>(options => options.MinLevel = logLevel);

            services.AddTransient<FunctionalTestCompiler>();
            services.AddTransient<Run6502FunctionalTest>();
        }

    }
}
