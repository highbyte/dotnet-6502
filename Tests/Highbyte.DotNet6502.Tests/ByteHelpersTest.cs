namespace Highbyte.DotNet6502.Tests;

public class ByteHelpersTest
{
    [Fact]
    public void ToHexAndDecimal_Can_Format_Output_Correctly()
    {
        // Arrange
        // Act
        var output = ByteHelpers.ToHexAndDecimal(0x42);

        // Assert
        Assert.Equal("0x42 (66)", output);
    }

    [Fact]
    public void ToDecimalAndHex_Can_Format_Output_Correctly()
    {
        // Arrange
        // Act
        var output = ByteHelpers.ToDecimalAndHex(0x42);

        // Assert
        Assert.Equal("66 (0x42)", output);
    }        

    // TODO: More unit tests for ByteHelpers class
}
