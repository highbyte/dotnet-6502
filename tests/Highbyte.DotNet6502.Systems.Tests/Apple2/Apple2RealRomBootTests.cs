using System.Text;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Input;
using Highbyte.DotNet6502.Systems.Apple2.Render;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// Boot-to-BASIC verification against a genuine Apple II Plus ROM.
///
/// The ROM is copyrighted and cannot be checked in, so these tests are opt-in: they skip unless
/// an image is found. Put <c>APPLE2.ROM</c> in the Apple II ROM directory
/// (<see cref="Apple2SystemConfig.DefaultROMDirectory"/>) or point
/// <c>DOTNET6502_APPLE2_ROM</c> at a file, then run:
/// <code>dotnet test --filter TestType=Integration</code>
/// The CI-friendly equivalent that needs no third-party ROM is
/// <see cref="Apple2SyntheticRomTests"/>.
/// </summary>
[Trait("TestType", "Integration")]
public class Apple2RealRomBootTests
{
    private const string RomPathEnvironmentVariable = "DOTNET6502_APPLE2_ROM";
    private const string CharacterRomPathEnvironmentVariable = "DOTNET6502_APPLE2_CHARGEN_ROM";
    private const string DefaultRomFileName = "APPLE2.ROM";
    private const string DefaultCharacterRomFileName = "3410036.BIN";

    /// <summary>Frames to run before the Autostart ROM has reached the Applesoft prompt.</summary>
    private const int BootFrames = 180;

    private readonly ITestOutputHelper _output;

    public Apple2RealRomBootTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Boots_To_The_Applesoft_Prompt()
    {
        var apple2 = BootRealRom();
        if (apple2 == null)
            return;

        Assert.True(apple2.HasBasicStarted());

        var screen = ReadScreen(apple2);
        Assert.Contains("APPLE ][", screen[0]);
        Assert.StartsWith("]", screen[2]);     // the Applesoft prompt
    }

    [Fact]
    public void Reset_Vector_Points_At_The_Autostart_Monitor()
    {
        var apple2 = BootRealRom(runFrames: 0);
        if (apple2 == null)
            return;

        Assert.Equal(0xFA62, apple2.CPU.PC);
    }

    [Fact]
    public void Typed_Input_Reaches_Applesoft_And_Is_Evaluated()
    {
        var apple2 = BootRealRom();
        if (apple2 == null)
            return;

        var inputState = new ScriptedHostInputState();
        var inputHandler = new Apple2InputHandler(apple2, NullLoggerFactory.Instance);
        inputHandler.Init(inputState);

        foreach (var key in new[]
                 {
                     HostKey.KeyP, HostKey.KeyR, HostKey.KeyI, HostKey.KeyN, HostKey.KeyT,
                     HostKey.Space, HostKey.Digit2, HostKey.Equal, HostKey.Digit3, HostKey.Enter,
                 })
        {
            // Shift on Equal produces '+'.
            TypeKey(apple2, inputHandler, inputState, key, shift: key == HostKey.Equal);
        }

        for (var frame = 0; frame < 30; frame++)
            apple2.ExecuteOneFrame();

        var screen = ReadScreen(apple2);
        Assert.Equal("]PRINT 2+3", screen[2].TrimEnd());
        Assert.Equal("5", screen[3].TrimEnd());
    }

    [Fact]
    public void The_Rasterizer_Draws_The_Banner_From_The_Real_Character_Generator()
    {
        var apple2 = BootRealRom();
        if (apple2 == null || apple2.CharacterRom == null)
        {
            _output.WriteLine("SKIPPED: no character generator ROM available.");
            return;
        }

        var rasterizer = (Apple2Rasterizer)apple2.RenderProviders.Single(p => p is Apple2Rasterizer);
        rasterizer.OnEndFrame();

        var lit = rasterizer.CurrentFrontLayerBuffers[1];
        Assert.Equal(Apple2Config.DrawableAreaWidth * Apple2Config.DrawableAreaHeight, lit.Length);

        // Row 0 holds the "APPLE ][" banner, so it must have lit pixels; row 1 is blank.
        Assert.True(RowHasLitPixels(rasterizer, textRow: 0), "Banner row should have lit pixels.");
        Assert.False(RowHasLitPixels(rasterizer, textRow: 1), "Row 1 should be blank.");

        _output.WriteLine(RenderRowAsText(rasterizer, textRow: 0));
    }

    private static bool RowHasLitPixels(Apple2Rasterizer rasterizer, int textRow)
    {
        for (var line = 0; line < Apple2Config.CharacterHeight; line++)
        {
            var offset = ((textRow * Apple2Config.CharacterHeight) + line) * rasterizer.NativeSize.Width;
            for (var x = 0; x < rasterizer.NativeSize.Width; x++)
                if (rasterizer.CurrentFrontLayerBuffers[1].Span[offset + x] != 0u)
                    return true;
        }
        return false;
    }

    private static string RenderRowAsText(Apple2Rasterizer rasterizer, int textRow)
    {
        var sb = new StringBuilder();
        for (var line = 0; line < Apple2Config.CharacterHeight; line++)
        {
            var offset = ((textRow * Apple2Config.CharacterHeight) + line) * rasterizer.NativeSize.Width;
            for (var x = 0; x < rasterizer.NativeSize.Width; x++)
                sb.Append(rasterizer.CurrentFrontLayerBuffers[1].Span[offset + x] != 0u ? '#' : ' ');
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>
    /// Boots the machine on a real ROM, or returns <c>null</c> when no ROM is available so the
    /// test can bow out. xUnit v2 has no run-time skip, so the reason is written to the test
    /// output instead of failing a build that legitimately has no ROM to test with.
    /// </summary>
    private Apple2System? BootRealRom(int runFrames = BootFrames)
    {
        var romPath = ResolveRomPath();
        if (romPath == null)
        {
            _output.WriteLine(
                $"SKIPPED: no Apple II ROM found. Set {RomPathEnvironmentVariable} or place {DefaultRomFileName} in " +
                $"{Apple2SystemConfig.DefaultROMDirectory}.");
            return null;
        }

        _output.WriteLine($"Using Apple II ROM: {romPath}");

        var romData = new Dictionary<string, byte[]>
        {
            { Apple2SystemConfig.SYSTEM_ROM_NAME, File.ReadAllBytes(romPath) },
        };

        var characterRomPath = ResolveCharacterRomPath();
        if (characterRomPath != null)
        {
            _output.WriteLine($"Using Apple II character generator ROM: {characterRomPath}");
            romData[Apple2SystemConfig.CHARGEN_ROM_NAME] = File.ReadAllBytes(characterRomPath);
        }

        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance, romData);

        for (var frame = 0; frame < runFrames; frame++)
            apple2.ExecuteOneFrame();

        return apple2;
    }

    private static string? ResolveRomPath()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(RomPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment))
            return fromEnvironment;

        var fromRomDirectory = Path.Combine(Apple2SystemConfig.DefaultROMDirectory, DefaultRomFileName);
        return File.Exists(fromRomDirectory) ? fromRomDirectory : null;
    }

    private static string? ResolveCharacterRomPath()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(CharacterRomPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment))
            return fromEnvironment;

        var fromRomDirectory = Path.Combine(Apple2SystemConfig.DefaultROMDirectory, DefaultCharacterRomFileName);
        return File.Exists(fromRomDirectory) ? fromRomDirectory : null;
    }

    private static void TypeKey(
        Apple2System apple2,
        Apple2InputHandler inputHandler,
        ScriptedHostInputState inputState,
        HostKey key,
        bool shift)
    {
        inputState.KeysDown = shift
            ? new HashSet<HostKey> { key, HostKey.ShiftLeft }
            : new HashSet<HostKey> { key };
        inputHandler.BeforeFrame();
        apple2.ExecuteOneFrame();

        inputState.KeysDown = new HashSet<HostKey>();
        inputHandler.BeforeFrame();
        apple2.ExecuteOneFrame();
    }

    private static string[] ReadScreen(Apple2System apple2)
    {
        var rows = new string[Apple2TextScreen.Rows];
        for (var row = 0; row < Apple2TextScreen.Rows; row++)
        {
            var sb = new StringBuilder(Apple2TextScreen.Columns);
            for (var col = 0; col < Apple2TextScreen.Columns; col++)
                sb.Append(Apple2CharSet.ScreenCodeToUnicode(apple2.Mem[Apple2TextScreen.GetAddress(row, col)]));
            rows[row] = sb.ToString();
        }
        return rows;
    }

    private sealed class ScriptedHostInputState : IHostInputState
    {
        public IReadOnlySet<HostKey> KeysDown { get; set; } = new HashSet<HostKey>();
        public IReadOnlySet<GamepadButton> GamepadButtonsDown { get; } = new HashSet<GamepadButton>();
        public bool CapsLockOn => false;

        public void UpdatePerFrame()
        {
        }
    }
}
