using System.Diagnostics.CodeAnalysis;
using Highbyte.DotNet6502.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: ExcludeFromCodeCoverage]
namespace Highbyte.DotNet6502.ConsoleTestPrograms
{
    class Program
    {
        //static void Main(string[] args)
        static void Main()
        {
            //// Download, compile, and run 6502 functional test program
            // var serviceCollection = new ServiceCollection();
            // ConfigureServices(serviceCollection);
            // var serviceProvider = serviceCollection.BuildServiceProvider(); 
            // var run6502FunctionalTest = serviceProvider.GetService<Run6502FunctionalTest>();
            // run6502FunctionalTest.Run();

            // Run simple machine code to add two numbers and rotate right
            // RunSimple.Run();

            // Run 16-bit multiplication 6502 binary
            //Run16bitMultiplyProgram.Run();

            // Run simple 6502 binary
            //RunTestProgram.Run();
            //RunTestProgram2.Run();

            //HostInteractionLab_Move_One_Char.Run();

            HostInteractionLab_Scroll_Text.Run();
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
