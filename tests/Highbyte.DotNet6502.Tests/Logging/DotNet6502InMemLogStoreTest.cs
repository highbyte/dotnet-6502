using Highbyte.DotNet6502.Logging;

namespace Highbyte.DotNet6502.Tests.Logging;

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
        for (int i = 0; i < memLogStore.MaxLogMessages; i++)
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
        for (int i = 0; i < memLogStore.MaxLogMessages; i++)
            memLogStore.WriteLog($"Old message {i}");

        // Act
        memLogStore.MaxLogMessages = 7;

        // Assert
        var actualMessages = memLogStore.GetLogMessages();
        Assert.Equal(7, actualMessages.Count);
        Assert.Equal("Old message 9", actualMessages[0]);
    }
}
