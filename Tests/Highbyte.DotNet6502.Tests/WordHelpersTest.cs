using Xunit;

namespace Highbyte.DotNet6502.Tests
{
    public class WordHelpersTest
    {
        [Fact]
        public void ToHexAndDecimal_Can_Format_Output_Correctly()
        {
            // Arrange
            // Act
            var output = WordHelpers.ToHexAndDecimal(0x2142);

            // Assert
            Assert.Equal("0x2142 (8514)", output);
        }

        [Fact]
        public void ToDecimalAndHex_Can_Format_Output_Correctly()
        {
            // Arrange
            // Act
            var output = WordHelpers.ToDecimalAndHex(0x2142);

            // Assert
            Assert.Equal("8514 (0x2142)", output);
        }        

        // TODO: More unit tests for WordHelpers class
    }
}
