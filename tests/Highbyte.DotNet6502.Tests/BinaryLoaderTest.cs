namespace Highbyte.DotNet6502.Tests;

public class BinaryLoaderTest
{
    private readonly ITestOutputHelper _output;
    public BinaryLoaderTest(ITestOutputHelper testOutputHelper)
    {
        _output = testOutputHelper;
    }

    [Fact]
    public void ReadFile_Can_Read_A_Binary_File()
    {
        // Arrange
        byte[] bytes = { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        var filePath = "test.bin";
        File.WriteAllBytes(filePath, bytes);

        var bytesString = string.Join(" ", bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        _output.WriteLine("Bytes saved:");
        _output.WriteLine(bytesString);


        // Act
        var loadedBytes = BinaryLoader.ReadFile(filePath, fileHeaderContainsLoadAddress: false, out _, out ushort codeAndDataFileSize);

        // Assert
        Assert.Equal(bytes.Length, loadedBytes.Length);
        Assert.Equal(bytes.Length, codeAndDataFileSize);

        Assert.Equivalent(bytes, loadedBytes);
    }

    [Fact]
    public void ReadFile_Can_Read_A_Binary_File_With_Address_Header()
    {
        // Arrange
        byte[] header = { 0x00, 0x01 };
        byte[] data = { 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        byte[] bytes = header.Concat(data).ToArray();
        var filePath = "test.bin";
        File.WriteAllBytes(filePath, bytes);

        var bytesString = string.Join(" ", bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        _output.WriteLine("Bytes saved:");
        _output.WriteLine(bytesString);

        // Act
        var loadedBytes = BinaryLoader.ReadFile(filePath, fileHeaderContainsLoadAddress: true, out ushort? fileHeaderLoadAddress, out ushort codeAndDataFileSize);

        // Assert
        Assert.Equal(data.Length , loadedBytes.Length);
        Assert.Equal(data.Length, codeAndDataFileSize);
        Assert.Equal((ushort)0x0100, fileHeaderLoadAddress);

        Assert.Equivalent(data, loadedBytes);
    }

}
