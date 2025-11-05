
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Tests.Logging;

public class DotNet6502InMemLogStoreTest
{
    [Fact]
    public void Writing_LogMessage_Stores_Message()
    {
        // Arrange
        var memLogStore = new DotNet6502InMemLogStore();
        var message = "Test message";

        // Act
        memLogStore.WriteLog(message);

        // Assert
        var actualMessages = memLogStore.GetLogMessages();
        Assert.Single(actualMessages);
        Assert.Equal(message, actualMessages[0]);
    }

    [Fact]
    public void Writing_LogMessage_Stores_Message_And_Writes_DebugMessage_If_Configured()
    {
        // Arrange
        var memLogStore = new DotNet6502InMemLogStore() { WriteDebugMessage = true };
        var message = "Test message";

        // Act
        memLogStore.WriteLog(message);

        // Assert
        Assert.True(memLogStore.WriteDebugMessage);
        // Cannot verify that an Debug.WriteLine is issued?

        var actualMessages = memLogStore.GetLogMessages();
        Assert.Single(actualMessages);
        Assert.Equal(message, actualMessages[0]);
    }

    [Fact]
    public void Writing_LogMessage_Inserts_It_At_The_Top_Of_The_List()
    {
        // Arrange
        var memLogStore = new DotNet6502InMemLogStore();
        var oldMessage = "Old message";
        memLogStore.WriteLog(oldMessage);
        var newMessage = "New message";

        // Act
        memLogStore.WriteLog(newMessage);

        // Assert
        var actualMessages = memLogStore.GetLogMessages();
        Assert.Equal(2, actualMessages.Count);
        Assert.Equal(newMessage, actualMessages[0]);
        Assert.Equal(oldMessage, actualMessages[1]);
    }

    [Fact]
    public void Writing_LogMessage_When_Store_Is_Full_Removes_The_Last_One_In_The_list()
    {
        // Arrange
        var memLogStore = new DotNet6502InMemLogStore();
        for (var i = 0; i < memLogStore.MaxLogMessages; i++)
            memLogStore.WriteLog($"Old message {i}");
        var newMessage = "New message";

        // Act
        memLogStore.WriteLog(newMessage);

        // Assert
        var actualMessages = memLogStore.GetLogMessages();

        Assert.Equal(memLogStore.MaxLogMessages, actualMessages.Count);
        Assert.Equal(newMessage, actualMessages[0]);
    }

    [Fact]
    public void Setting_MaxLogMessages_To_Less_Than_Zero_Throws_Exception()
    {
        // Arrange
        var memLogStore = new DotNet6502InMemLogStore();

        // Act/Assert
        Assert.Throws<ArgumentException>(() => memLogStore.MaxLogMessages = -1);
    }

    [Fact]
    public void Changing_The_Maxium_Size_To_A_Lower_Value_Than_Current_Length_Removes_The_Overflow_At_Bottom_Of_List()
    {
        // Arrange
        var memLogStore = new DotNet6502InMemLogStore();
        memLogStore.MaxLogMessages = 10;
        for (var i = 0; i < memLogStore.MaxLogMessages; i++)
            memLogStore.WriteLog($"Old message {i}");

        // Act
        memLogStore.MaxLogMessages = 7;

        // Assert
        var actualMessages = memLogStore.GetLogMessages();
        Assert.Equal(7, actualMessages.Count);
        Assert.Equal("Old message 9", actualMessages[0]);
    }

    [Fact]
    public void Writing_LogMessage_With_LogLevel_Stores_Message_And_LogLevel()
    {
        // Arrange
        var memLogStore = new DotNet6502InMemLogStore();
        var message = "Test message";
        var logLevel = LogLevel.Warning;

        // Act
        memLogStore.WriteLog(logLevel, message);

        // Assert
        var actualMessages = memLogStore.GetLogMessages();
        Assert.Single(actualMessages);
        Assert.Equal(message, actualMessages[0]);

        var fullMessages = memLogStore.GetFullLogMessages();
        Assert.Single(fullMessages);
        Assert.Equal(logLevel, fullMessages[0].LogLevel);
        Assert.Equal(message, fullMessages[0].Message);
    }

    [Fact]
    public void Writing_LogMessage_Without_LogLevel_Uses_Information_Level()
    {
        // Arrange
        var memLogStore = new DotNet6502InMemLogStore();
        var message = "Test message";

        // Act
        memLogStore.WriteLog(message);

        // Assert
        var fullMessages = memLogStore.GetFullLogMessages();
        Assert.Single(fullMessages);
        Assert.Equal(LogLevel.Information, fullMessages[0].LogLevel);
        Assert.Equal(message, fullMessages[0].Message);
    }

    [Fact]
    public void GetLogMessages_Returns_Only_Messages_For_Backward_Compatibility()
    {
        // Arrange
        var memLogStore = new DotNet6502InMemLogStore();
        var message1 = "Error message";
        var message2 = "Warning message";

        // Act
        memLogStore.WriteLog(LogLevel.Error, message1);
        memLogStore.WriteLog(LogLevel.Warning, message2);

        // Assert - Messages are inserted at the start, so latest message is first
        var messages = memLogStore.GetLogMessages();
        Assert.Equal(2, messages.Count);
        Assert.Equal(message2, messages[0]); // Latest message is first due to insertAtStart=true
        Assert.Equal(message1, messages[1]);
        Assert.True(messages.All(m => m is string)); // Ensure it's just strings
    }

    [Fact]
    public void GetFullLogMessages_Returns_LogLevel_And_Messages()
    {
        // Arrange
        var memLogStore = new DotNet6502InMemLogStore();
        var message1 = "Error message";
        var message2 = "Warning message";

        // Act
        memLogStore.WriteLog(LogLevel.Error, message1);
        memLogStore.WriteLog(LogLevel.Warning, message2);

        // Assert - Messages are inserted at the start, so latest message is first
        var fullMessages = memLogStore.GetFullLogMessages();
        Assert.Equal(2, fullMessages.Count);
        Assert.Equal(LogLevel.Warning, fullMessages[0].LogLevel); // Latest message is first
        Assert.Equal(message2, fullMessages[0].Message);
        Assert.Equal(LogLevel.Error, fullMessages[1].LogLevel);
        Assert.Equal(message1, fullMessages[1].Message);
    }

    [Fact]
    public void LogMessageAdded_Event_Provides_LogEntry_With_LogLevel_And_Message()
    {
        // Arrange
        var memLogStore = new DotNet6502InMemLogStore();
        var message = "Test message";
        var logLevel = LogLevel.Warning;
        LogEntry? receivedLogEntry = null;

        memLogStore.LogMessageAdded += (sender, logEntry) =>
        {
            receivedLogEntry = logEntry;
        };

        // Act
        memLogStore.WriteLog(logLevel, message);

        // Assert
        Assert.NotNull(receivedLogEntry);
        Assert.Equal(logLevel, receivedLogEntry.LogLevel);
        Assert.Equal(message, receivedLogEntry.Message);
    }
}
