using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Tests.Utils;

public class StreamHelperTest
{

    [Fact]
    public void FetchWord_Returns_LittleEndian_Word_From_Stream()
    {
        // Arrange
        var storedData = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };


        // Act
        int readWord;
        using (MemoryStream ms = new MemoryStream(storedData))
        {
            readWord = ms.FetchWord();
        }

        // Assert
        Assert.Equal(0x0100, readWord);
    }

    [Fact]
    public void FetchWord_Returns_Negative_If_No_Byte_Left_In_Stream()
    {
        // Arrange
        var storedData = new byte[] { };

        // Act
        int readWord;
        using (MemoryStream ms = new MemoryStream(storedData))
        {
            readWord = ms.FetchWord();
        }

        // Assert
        Assert.Equal(-1, readWord);
    }

    [Fact]
    public void FetchWord_Returns_Negative_If_Only_One_Byte_Left_In_Stream()
    {
        // Arrange
        var storedData = new byte[] { 0 };

        // Act
        int readWord;
        using (MemoryStream ms = new MemoryStream(storedData))
        {
            readWord = ms.FetchWord();
        }

        // Assert
        Assert.Equal(-1, readWord);
    }
}
