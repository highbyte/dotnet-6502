using System.Text;
using Highbyte.DotNet6502.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Highbyte.DotNet6502.Tests.Logging;

public class DotNet6502LoggerBaseTest
{
    [Theory]
    [InlineData(LogLevel.Trace, "[trce]")]
    [InlineData(LogLevel.Debug, "[dbug]")]
    [InlineData(LogLevel.Information, "[info]")]
    [InlineData(LogLevel.Warning, "[warn]")]
    [InlineData(LogLevel.Error, "[fail]")]
    [InlineData(LogLevel.Critical, "[crit]")]
    public void Writing_To_Log_Will_Create_Correctly_Formatted_LogLevel(LogLevel logLevel, string expectedLogLevelString)
    {
        // Arrange
        var category = "TestCategory";
        var message = "Test message";
        var logger = new FakeLogger(new DefaultObjectPoolProvider().CreateStringBuilderPool(), category);

        // Act
        logger.Log(logLevel, message);

        // Assert
        // Expected format example: 21:26:34.043 [info] Category Message
        Assert.Single(logger.LogMessages);
        var actualLogLevel = GetLogLevelPart(logger.LogMessages[0]);
        Assert.Equal(expectedLogLevelString, actualLogLevel);
    }

    [Fact]
    public void Writing_To_Log_Will_Create_Correctly_Formatted_CategoryPart()
    {
        // Arrange
        var category = "TestCategory";
        var message = "Test message";
        var logger = new FakeLogger(new DefaultObjectPoolProvider().CreateStringBuilderPool(), category);

        // Act
        logger.LogInformation(message);

        // Assert
        // Expected format example: 21:26:34.043 [info] Category Message
        Assert.Single(logger.LogMessages);
        var categoryPart = GetCategoryPart(logger.LogMessages[0]);
        Assert.Equal(category, categoryPart);
    }

    [Fact]
    public void Writing_To_Log_Will_Create_Correctly_Formatted_MessagePart()
    {
        // Arrange
        var category = "TestCategory";
        var message = "Test message";
        var logger = new FakeLogger(new DefaultObjectPoolProvider().CreateStringBuilderPool(), category);

        // Act
        logger.LogInformation(message);

        // Assert
        // Expected format example: 21:26:34.043 [info] Category Message
        Assert.Single(logger.LogMessages);
        var messagePart = GetMessagePart(logger.LogMessages[0]);
        Assert.Equal(message, messagePart);
    }

    //private string GetTimePart(string formattedMessage)
    //{
    //    var messagePartArray = formattedMessage.Split(' ');

    //    var time = messagePartArray switch
    //    {
    //        [var tim, ..] => tim,
    //        _ => throw new Exception($"Log has incorrect format: {formattedMessage}"),
    //    };
    //    return time;
    //}

    private string GetLogLevelPart(string formattedMessage)
    {
        var messagePartArray = formattedMessage.Split(' ');

        var logLevel = messagePartArray switch
        {
            [_, var level, ..] => level,
            _ => throw new Exception($"Log has incorrect format: {formattedMessage}"),
        };
        return logLevel;
    }

    private string GetCategoryPart(string formattedMessage)
    {
        var messagePartArray = formattedMessage.Split(' ');

        var category = messagePartArray switch
        {
            [_, _, var cat, ..] => cat,
            _ => throw new Exception($"Log has incorrect format: {formattedMessage}"),
        };
        return category;
    }

    private string GetMessagePart(string formattedMessage)
    {
        var messagePartArray = formattedMessage.Split(' ');

        string[] messageParts = messagePartArray switch
        {
            [_, _, _, .. string[] msgParts] => msgParts,
            _ => throw new Exception($"Log has incorrect format: {formattedMessage}"),
        };

        var messagePartsString = string.Join(" ", messageParts);
        if (messagePartsString.Contains('\r'))
            messagePartsString = messagePartsString[..messagePartsString.IndexOf('\r')];
        else if (messagePartsString.Contains('\n'))
            messagePartsString = messagePartsString[..messagePartsString.IndexOf('\n')];

        return messagePartsString;
    }
}

public class FakeLogger : DotNet6502LoggerBase
{
    public List<string> LogMessages { get; } = new();
    public FakeLogger(ObjectPool<StringBuilder> stringBuilderPool, string categoryName) : base(stringBuilderPool, categoryName)
    {
    }

    public override bool IsEnabled(LogLevel logLevel) => true;

    public override void WriteLog(string message)
    {
        LogMessages.Add(message);
    }
}
