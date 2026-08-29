using System.Text;
using Highbyte.DotNet6502.Systems.Oric.Tape;
using Highbyte.DotNet6502.Utils;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class OricTapParserTests
{
    private const string OricBasicSamplesDirectory =
        "../../../../../samples/Basic/Oric/Text/Build";

    [Fact]
    public void Bas2TapHelloWorldParsesAsBasicAtTheStandardAddress()
    {
        var tapFile = OricTapParser.Parse(File.ReadAllBytes(GetSamplePath("HelloWorld")));

        Assert.Equal("HelloWorld", tapFile.Name);
        Assert.True(tapFile.IsBasic);
        Assert.False(tapFile.IsAutoRun);
        Assert.Equal(OricMachine.BasicProgramDefaultStartAddress, tapFile.StartAddress);
        Assert.Equal(0x051d, tapFile.EndAddress);
        Assert.Equal(29, tapFile.Data.Length);
    }

    [Fact]
    public void LoaderCopiesTheProgramAndInitializesBasicPointers()
    {
        var oric = new OricMachine();

        var tapFile = oric.LoadBasicTap(File.ReadAllBytes(GetSamplePath("HelloWorld")));

        Assert.Equal(tapFile.Data, oric.Mem.ReadData(tapFile.StartAddress, (ushort)tapFile.Data.Length));
        Assert.Equal(tapFile.StartAddress, oric.GetBasicProgramStartAddress());
        Assert.Equal(tapFile.EndAddress, oric.GetBasicProgramEndAddress());
        Assert.Equal(tapFile.EndAddress, oric.Mem.FetchWord(OricMachine.BasicArrayStartPointerAddress));
        Assert.Equal(tapFile.EndAddress, oric.Mem.FetchWord(OricMachine.BasicFreeMemoryStartPointerAddress));
        Assert.Equal("10 CLS\n20 PRINT\"HELLO WORLD!\"\n", oric.BasicTokenParser.GetBasicText().ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData("AySoundLab", "230 PRINT\"DONE\"")]
    [InlineData("Fireworks", "120 PRINT\"FIREWORKS!\"")]
    [InlineData("HelloWorld", "20 PRINT\"HELLO WORLD!\"")]
    [InlineData("HiresShapes", "140 PRINT\"HIRES SHAPES\"")]
    [InlineData("SoundEffects", "80 PRINT\"DONE\"")]
    [InlineData("ThreeVoiceMusic", "150 DATA 8,12,3,1,5,8")]
    public void BuiltBasicExamplesCanBeLoadedAndDetokenized(string sampleName, string expectedLine)
    {
        var oric = new OricMachine();

        var tapFile = oric.LoadBasicTap(File.ReadAllBytes(GetSamplePath(sampleName)));

        Assert.Equal(sampleName, tapFile.Name);
        Assert.Contains(expectedLine, oric.BasicTokenParser.GetBasicText());
    }

    [Theory]
    [InlineData("AySoundLab", "70 SOUND 1,P,15", "140 SOUND 4,P,15")]
    [InlineData("ThreeVoiceMusic", "70 MUSIC 1,4,A,15", "90 MUSIC 3,4,C,15")]
    public void BuiltSoundExamplesUseMaximumFixedVolume(
        string sampleName,
        string expectedFirstVolumeLine,
        string expectedLastVolumeLine)
    {
        var oric = new OricMachine();
        oric.LoadBasicTap(File.ReadAllBytes(GetSamplePath(sampleName)));

        var source = oric.BasicTokenParser.GetBasicText();

        Assert.Contains(expectedFirstVolumeLine, source);
        Assert.Contains(expectedLastVolumeLine, source);
    }

    [Fact]
    public void FourSyncByteHeaderAndAutoRunFlagAreAccepted()
    {
        var tapData = BuildTap(syncByteCount: 4, autoRunFlag: 0x80);

        var tapFile = OricTapParser.Parse(tapData);

        Assert.Equal("TEST", tapFile.Name);
        Assert.True(tapFile.IsAutoRun);
        Assert.Equal(new byte[] { 0, 0, 0 }, tapFile.Data);
    }

    [Fact]
    public void MachineCodeTapeIsRejectedByTheBasicLoader()
    {
        var oric = new OricMachine();
        var tapData = BuildTap(fileType: 0x80);

        var exception = Assert.Throws<InvalidDataException>(() => oric.LoadBasicTap(tapData));

        Assert.Contains("not a BASIC program", exception.Message);
    }

    [Fact]
    public void BasicTapeAtANonstandardAddressIsRejected()
    {
        var oric = new OricMachine();
        var tapData = BuildTap(startAddress: 0x0601);

        var exception = Assert.Throws<InvalidDataException>(() => oric.LoadBasicTap(tapData));

        Assert.Contains("must load at $0501", exception.Message);
    }

    [Theory]
    [MemberData(nameof(InvalidTapImages))]
    public void InvalidTapeImagesAreRejected(byte[] tapData, string expectedMessage)
    {
        var exception = Assert.Throws<InvalidDataException>(() => OricTapParser.Parse(tapData));

        Assert.Contains(expectedMessage, exception.Message);
    }

    public static TheoryData<byte[], string> InvalidTapImages => new()
    {
        { Array.Empty<byte>(), "sync bytes" },
        { new byte[] { 0x16, 0x16, 0x24 }, "sync bytes" },
        { new byte[] { 0x16, 0x16, 0x16, 0x24, 0x00 }, "header is truncated" },
        { BuildTap()[..^1], "payload is truncated" },
        { BuildTap(includeFileNameTerminator: false), "filename is not terminated" },
        { BuildTap(startAddress: 0x0600, endAddressOverride: 0x0500), "address range is invalid" },
    };

    private static byte[] BuildTap(
        int syncByteCount = 3,
        byte fileType = OricTapFile.BasicFileType,
        byte autoRunFlag = 0,
        ushort startAddress = OricMachine.BasicProgramDefaultStartAddress,
        ushort? endAddressOverride = null,
        bool includeFileNameTerminator = true)
    {
        byte[] payload = [0, 0, 0];
        var endAddress = endAddressOverride ?? (ushort)(startAddress + payload.Length - 1);
        var bytes = new List<byte>();
        bytes.AddRange(Enumerable.Repeat(OricTapParser.SyncByte, syncByteCount));
        bytes.Add(OricTapParser.HeaderMarker);
        bytes.AddRange([
            0x00,
            0x00,
            fileType,
            autoRunFlag,
            (byte)(endAddress >> 8),
            (byte)endAddress,
            (byte)(startAddress >> 8),
            (byte)startAddress,
            0x00,
        ]);
        bytes.AddRange(Encoding.ASCII.GetBytes("TEST"));
        if (includeFileNameTerminator)
        {
            bytes.Add(0);
            bytes.AddRange(payload);
        }
        return bytes.ToArray();
    }

    private static string GetSamplePath(string sampleName)
        => Path.Combine(OricBasicSamplesDirectory, $"{sampleName}.tap");
}
