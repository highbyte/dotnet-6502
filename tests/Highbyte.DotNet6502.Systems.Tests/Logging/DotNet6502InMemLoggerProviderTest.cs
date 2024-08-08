using Highbyte.DotNet6502.Systems.Logging.InMem;

namespace Highbyte.DotNet6502.Systems.Tests.Logging;

public class DotNet6502InMemLoggerProviderTest
{
    [Fact]
    public void Can_Create_LoggerProvider()
    {
        // Arrange
        var config = new DotNet6502InMemLoggerConfiguration(new DotNet6502InMemLogStore());

        // Act
        var loggerProvider = new DotNet6502InMemLoggerProvider(config);

        // Assert
    }

    [Fact]
    public void Can_Create_Logger_From_LoggerProvider()
    {
        // Arrange
        var config = new DotNet6502InMemLoggerConfiguration(new DotNet6502InMemLogStore());
        var loggerProvider = new DotNet6502InMemLoggerProvider(config);

        // Act
        var logger = loggerProvider.CreateLogger("TestCategory");

        // Assert
        Assert.NotNull(logger);
        Assert.IsType<DotNet6502InMemLogger>(logger);
    }
}
