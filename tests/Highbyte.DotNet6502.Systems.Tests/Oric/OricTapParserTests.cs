using System.Text;
using Highbyte.DotNet6502.Systems.Oric.Config;
using Highbyte.DotNet6502.Systems.Oric.Tape;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;
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
    public void MultiFileImageParsesEveryRecordAcrossTapePadding()
    {
        var first = BuildTap(name: "FIRST", payload: [0x01, 0x02]);
        var second = BuildTap(
            fileType: OricTapFile.MachineCodeFileType,
            startAddress: 0x0600,
            name: "SECOND",
            payload: [0xaa, 0xbb, 0xcc]);
        byte[] tapData = [.. first, 0x55, 0x00, .. second];

        var files = OricTapParser.ParseAll(tapData);

        Assert.Collection(
            files,
            file =>
            {
                Assert.Equal("FIRST", file.Name);
                Assert.True(file.IsBasic);
                Assert.Equal(new byte[] { 0x01, 0x02 }, file.Data);
            },
            file =>
            {
                Assert.Equal("SECOND", file.Name);
                Assert.True(file.IsMachineCode);
                Assert.Equal(new byte[] { 0xaa, 0xbb, 0xcc }, file.Data);
            });
    }

    [Fact]
    public void DirectLoaderLoadsMachineCodeWithoutAutoRunningIt()
    {
        var oric = new OricMachine();
        oric.CPU.PC = 0x1234;
        var tapData = BuildTap(
            fileType: OricTapFile.MachineCodeFileType,
            startAddress: 0x0600,
            payload: [0xea, 0x60]);

        var tapFile = oric.LoadTap(tapData);

        Assert.True(tapFile.IsMachineCode);
        Assert.Equal(new byte[] { 0xea, 0x60 }, oric.Mem.ReadData(0x0600, 2));
        Assert.Equal(0x1234, oric.CPU.PC);
    }

    [Fact]
    public void DirectLoaderStartsAutoRunMachineCodeAtItsLoadAddress()
    {
        var oric = new OricMachine();
        var tapData = BuildTap(
            fileType: OricTapFile.MachineCodeFileType,
            autoRunFlag: 0x80,
            startAddress: 0x0600,
            payload: [0x60]);

        oric.LoadTap(tapData);

        Assert.Equal(0x0600, oric.CPU.PC);
    }

    [Fact]
    public void DirectLoaderQueuesRunForAnAutoRunBasicProgram()
    {
        var oric = new OricMachine();
        var tapData = BuildTap(autoRunFlag: 0x80);

        oric.LoadTap(tapData);
        oric.ExecuteOneFrame();

        Assert.Equal((byte)('R' | 0x80), oric.Mem[OricMachine.KeyboardCharacterLatchAddress]);
    }

    [Fact]
    public void InsertedTapeIsValidatedAndCanBeRewoundOrEjected()
    {
        var oric = new OricMachine();
        var tapData = BuildTap();

        var files = oric.InsertTape(tapData);

        Assert.Single(files);
        Assert.True(oric.Tape.IsInserted);
        Assert.Equal(tapData.Length, oric.Tape.Length);
        Assert.Equal(0, oric.Tape.Position);

        oric.RewindTape();
        Assert.Equal(0, oric.Tape.Position);

        oric.EjectTape();
        Assert.False(oric.Tape.IsInserted);
        Assert.Empty(oric.Tape.Files);
    }

    [Fact]
    public void AtmosRomTapeBridgeReadsFromTheInsertedTapStream()
    {
        var oric = BuildOricWithTapeRoutineReturns();
        oric.InsertTape(BuildTap());
        oric.CPU.X = 0x7f;
        oric.CPU.Y = 0x6a;
        oric.Mem[0x02b1] = 0x5a;

        ExecuteHookAsSubroutine(oric, 0xe735);

        Assert.Equal(0, oric.Tape.Position);
        Assert.Equal(OricTapParser.SyncByte, oric.CPU.A);
        Assert.Equal(0, oric.CPU.X);
        Assert.Equal(0x6a, oric.CPU.Y);
        Assert.True(oric.CPU.ProcessorStatus.Carry);
        Assert.True(oric.CPU.ProcessorStatus.Zero);
        Assert.False(oric.CPU.ProcessorStatus.Negative);

        ExecuteHookAsSubroutine(oric, 0xe6c9);

        Assert.Equal(1, oric.Tape.Position);
        Assert.Equal(OricTapParser.SyncByte, oric.CPU.A);
        Assert.Equal(OricTapParser.SyncByte, oric.Mem[0x002f]);
        Assert.Equal(0x5a, oric.Mem[0x02b1]);
        Assert.False(oric.CPU.ProcessorStatus.Carry);
        Assert.False(oric.CPU.ProcessorStatus.Zero);
        Assert.False(oric.CPU.ProcessorStatus.Negative);
    }

    [Fact]
    public void AtmosRomTapeBridgeReturnsSuccessfulLdaStatusForEveryByte()
    {
        var oric = BuildOricWithTapeRoutineReturns();
        var tapData = BuildTap(
            fileType: OricTapFile.MachineCodeFileType,
            autoRunFlag: 0x80,
            payload: [0x00, 0x7f, 0x80, 0xff]);
        oric.InsertTape(tapData);
        oric.CPU.X = 0x34;
        oric.CPU.Y = 0x56;

        foreach (var expected in tapData)
        {
            oric.CPU.ProcessorStatus.Carry = true;
            oric.CPU.ProcessorStatus.Zero = expected != 0;
            oric.CPU.ProcessorStatus.Negative = (expected & 0x80) == 0;

            ExecuteHookAsSubroutine(oric, 0xe6c9);

            Assert.Equal(expected, oric.CPU.A);
            Assert.False(oric.CPU.ProcessorStatus.Carry);
            Assert.Equal(expected == 0, oric.CPU.ProcessorStatus.Zero);
            Assert.Equal((expected & 0x80) != 0, oric.CPU.ProcessorStatus.Negative);
            Assert.Equal(0x34, oric.CPU.X);
            Assert.Equal(0x56, oric.CPU.Y);
        }
    }

    [Fact]
    public void AtmosRomTapeBridgeKeepsItsCursorForTheNextFile()
    {
        var oric = BuildOricWithTapeRoutineReturns();
        var first = BuildTap(name: "FIRST", payload: [0x01]);
        var second = BuildTap(
            fileType: OricTapFile.MachineCodeFileType,
            startAddress: 0x0600,
            name: "SECOND",
            payload: [0x02]);
        oric.InsertTape([.. first, .. second]);

        for (var index = 0; index < first.Length; index++)
            ExecuteHookAsSubroutine(oric, 0xe6c9);

        ExecuteHookAsSubroutine(oric, 0xe735);
        Assert.Equal(first.Length, oric.Tape.Position);

        ExecuteHookAsSubroutine(oric, 0xe6c9);
        Assert.Equal(OricTapParser.SyncByte, oric.CPU.A);
        Assert.Equal(first.Length + 1, oric.Tape.Position);
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
        bool includeFileNameTerminator = true,
        string name = "TEST",
        byte[]? payload = null)
    {
        payload ??= [0, 0, 0];
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
        bytes.AddRange(Encoding.ASCII.GetBytes(name));
        if (includeFileNameTerminator)
        {
            bytes.Add(0);
            bytes.AddRange(payload);
        }
        return bytes.ToArray();
    }

    private static OricMachine BuildOricWithTapeRoutineReturns()
    {
        var rom = new byte[OricMachine.SystemRomSize];
        rom[0xe759 - OricMachine.SystemRomStartAddress] = 0x60;
        rom[0xe6fb - OricMachine.SystemRomStartAddress] = 0x60;
        return new OricMachine(
            new Highbyte.DotNet6502.Systems.Oric.OricConfig(),
            NullLoggerFactory.Instance,
            new Dictionary<string, byte[]> { [OricSystemConfig.SystemRomName] = rom });
    }

    private static void ExecuteHookAsSubroutine(OricMachine oric, ushort hookAddress)
    {
        const ushort returnAddress = 0x2000;
        var stackedAddress = (ushort)(returnAddress - 1);
        oric.CPU.SP = 0xfd;
        oric.Mem[0x01fe] = (byte)stackedAddress;
        oric.Mem[0x01ff] = (byte)(stackedAddress >> 8);
        oric.CPU.PC = hookAddress;

        oric.ExecuteOneInstruction(out _);

        Assert.Equal(returnAddress, oric.CPU.PC);
    }

    private static string GetSamplePath(string sampleName)
        => Path.Combine(OricBasicSamplesDirectory, $"{sampleName}.tap");
}
