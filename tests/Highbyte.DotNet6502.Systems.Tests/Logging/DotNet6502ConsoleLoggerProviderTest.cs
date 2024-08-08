using Highbyte.DotNet6502.Systems.Logging.Console;

namespace Highbyte.DotNet6502.Systems.Tests.Logging;

public class DotNet6502ConsoleLoggerProviderTest
{
    [Fact]
    public void Can_Create_LoggerProvider()
    {
        // Arrange
        var config = new DotNet6502ConsoleLoggerConfiguration();

        // Act
        var loggerProvider = new DotNet6502ConsoleLoggerProvider(config);

        // Assert
    }

    [Fact]
    public void Can_Create_Logger_From_LoggerProvider()
    {
        // Arrange
        var config = new DotNet6502ConsoleLoggerConfiguration();
        var loggerProvider = new DotNet6502ConsoleLoggerProvider(config);

        // Act
        var logger = loggerProvider.CreateLogger("TestCategory");

        // Assert
        Assert.NotNull(logger);
        Assert.IsType<DotNet6502ConsoleLogger>(logger);
    }
}
