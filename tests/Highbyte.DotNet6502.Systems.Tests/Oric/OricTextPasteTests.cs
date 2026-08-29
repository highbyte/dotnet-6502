using Highbyte.DotNet6502.Systems.Oric.Config;
using Microsoft.Extensions.Logging.Abstractions;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class OricTextPasteTests
{
    private static OricMachine BuildIdleMachine()
    {
        var rom = Enumerable.Repeat((byte)0xea, OricMachine.SystemRomSize).ToArray();
        // An endless loop leaves the keyboard latch untouched while frames continue to execute.
        rom[0] = 0x4c; // JMP $C000
        rom[1] = 0x00;
        rom[2] = 0xc0;
        rom[^4] = 0x00;
        rom[^3] = 0xc0;
        return new OricMachine(
            new(),
            NullLoggerFactory.Instance,
            new Dictionary<string, byte[]> { [OricSystemConfig.SystemRomName] = rom });
    }

    [Fact]
    public void PastedCharactersAreLatchedOnePerFrameWithValidBitSet()
    {
        var oric = BuildIdleMachine();
        oric.TextPaste.Paste("Ab");

        oric.ExecuteOneFrame();
        Assert.Equal(0xc1, oric.Mem[OricMachine.KeyboardCharacterLatchAddress]);

        // The next character waits while the first one remains valid.
        oric.ExecuteOneFrame();
        Assert.Equal(0xc1, oric.Mem[OricMachine.KeyboardCharacterLatchAddress]);

        oric.Mem[OricMachine.KeyboardCharacterLatchAddress] = 0;
        oric.ExecuteOneFrame();
        Assert.Equal(0xe2, oric.Mem[OricMachine.KeyboardCharacterLatchAddress]);
    }

    [Theory]
    [InlineData("A\nB")]
    [InlineData("A\rB")]
    [InlineData("A\r\nB")]
    public void HostLineEndingsBecomeOneOricReturn(string text)
    {
        var oric = BuildIdleMachine();
        oric.TextPaste.Paste(text);

        oric.ExecuteOneFrame();
        Assert.Equal(0xc1, oric.Mem[OricMachine.KeyboardCharacterLatchAddress]);

        oric.Mem[OricMachine.KeyboardCharacterLatchAddress] = 0;
        oric.ExecuteOneFrame();
        Assert.Equal(0x8d, oric.Mem[OricMachine.KeyboardCharacterLatchAddress]);

        oric.Mem[OricMachine.KeyboardCharacterLatchAddress] = 0;
        oric.ExecuteOneFrame();
        Assert.Equal(0xc2, oric.Mem[OricMachine.KeyboardCharacterLatchAddress]);
    }

    [Fact]
    public void UnsupportedCharactersAreDropped()
    {
        var oric = BuildIdleMachine();
        oric.TextPaste.Paste("A£B");

        oric.ExecuteOneFrame();
        Assert.Equal(0xc1, oric.Mem[OricMachine.KeyboardCharacterLatchAddress]);

        oric.Mem[OricMachine.KeyboardCharacterLatchAddress] = 0;
        oric.ExecuteOneFrame(); // drops £
        Assert.Equal(0, oric.Mem[OricMachine.KeyboardCharacterLatchAddress]);

        oric.ExecuteOneFrame();
        Assert.Equal(0xc2, oric.Mem[OricMachine.KeyboardCharacterLatchAddress]);
    }

    [Fact]
    public void ResetCancelsPendingPaste()
    {
        var oric = BuildIdleMachine();
        oric.TextPaste.Paste("AB");
        oric.ExecuteOneFrame();
        Assert.NotEqual(0, oric.Mem[OricMachine.KeyboardCharacterLatchAddress]);

        oric.Reset();

        oric.ExecuteOneFrame();

        Assert.Equal(0, oric.Mem[OricMachine.KeyboardCharacterLatchAddress]);
    }
}
