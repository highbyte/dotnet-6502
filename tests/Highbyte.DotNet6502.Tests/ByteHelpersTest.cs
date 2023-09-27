namespace Highbyte.DotNet6502.Tests;

public class ByteHelpersTest
{
    private readonly ITestOutputHelper _output;
    public ByteHelpersTest(ITestOutputHelper testOutputHelper)
    {
        _output = testOutputHelper;
    }

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

    [Fact]
    public void Can_Shift_Bits_In_Byte_Array_Right()
    {
        // Arrange
        var bytes = new byte[]
        {
            0b11000011, 0b00001111, 0b01010101, 0b00110011
        };

        var bytesString = string.Join(" ", bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        _output.WriteLine("Bytes (original)");
        _output.WriteLine(bytesString);

        int shiftRightBits = 3;

        // Act
        var shiftedBytes = bytes.ShiftRight(shiftRightBits, out _);

        var bytesResultString = string.Join(" ", shiftedBytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        _output.WriteLine($"Bytes after shift right {shiftRightBits}");
        _output.WriteLine(bytesResultString);

        // Assert
        var expectedBytesAfterShiftRight = new byte[]
        {
            0b00011000, 0b01100001, 0b11101010, 0b10100110
        };
        var bytesExpectedString = string.Join(" ", expectedBytesAfterShiftRight.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        _output.WriteLine($"Bytes expected after shift right {shiftRightBits}");
        _output.WriteLine(bytesExpectedString);

        foreach (var (expectedByte, actualByte) in expectedBytesAfterShiftRight.Zip(shiftedBytes))
            Assert.Equal(expectedByte, actualByte);
    }

    // TODO: More unit tests for ByteHelpers class
}
