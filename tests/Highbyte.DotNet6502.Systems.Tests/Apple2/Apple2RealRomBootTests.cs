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
/// The ROM is copyrighted and cannot be checked in, so these tests are opt-in: without one they
/// report as <em>skipped</em>, with the reason, rather than silently passing — see
/// <see cref="Apple2TestRoms"/>. Put the ROMs in the Apple II ROM directory
/// (<see cref="Apple2SystemConfig.DefaultROMDirectory"/>) under the names the archive publishes
/// them with — which is exactly what the app's own ROM download writes — or point
/// <c>DOTNET6502_APPLE2_ROM</c> at a file, then run:
/// <code>dotnet test --filter TestType=Integration</code>
/// The CI-friendly equivalent that needs no third-party ROM is
/// <see cref="Apple2SyntheticRomTests"/>.
/// </summary>
[Trait("TestType", "Integration")]
public class Apple2RealRomBootTests
{
    /// <summary>Frames to run before the Autostart ROM has reached the Applesoft prompt.</summary>
    private const int BootFrames = 180;

    private readonly ITestOutputHelper _output;

    public Apple2RealRomBootTests(ITestOutputHelper output) => _output = output;

    [RequiresApple2RomFact]
    public void Boots_To_The_Applesoft_Prompt()
    {
        var apple2 = BootRealRom();

        Assert.True(apple2.HasBasicStarted());

        var screen = ReadScreen(apple2);
        Assert.Contains("APPLE ][", screen[0]);
        Assert.StartsWith("]", screen[2]);     // the Applesoft prompt
    }

    [RequiresApple2RomFact]
    public void Reset_Vector_Points_At_The_Autostart_Monitor()
    {
        var apple2 = BootRealRom(runFrames: 0);

        Assert.Equal(0xFA62, apple2.CPU.PC);
    }

    /// <summary>
    /// A hand-tokenized <c>10 PRINT 3</c>: line link, line number 10, PRINT token ($BA), '3',
    /// end-of-line, then the $00 $00 end-of-program link.
    /// </summary>
    private static readonly byte[] s_tokenizedPrint3 =
    {
        0x08, 0x08,     // link to next line ($0808)
        0x0A, 0x00,     // line number 10
        0xBA,           // PRINT
        0x33,           // '3'
        0x00,           // end of line
        0x00, 0x00,     // end of program
    };

    [RequiresApple2RomFact]
    public void Injected_Basic_Program_Runs_And_Lists()
    {
        var apple2 = BootRealRom();

        // Inject the tokenized program the way any external loader would: place the bytes at
        // $0801 and initialise the Applesoft zero-page pointers.
        for (var i = 0; i < s_tokenizedPrint3.Length; i++)
            apple2.Mem[(ushort)(Apple2System.BASIC_LOAD_ADDRESS + i)] = s_tokenizedPrint3[i];
        apple2.InitBasicMemoryVariables(Apple2System.BASIC_LOAD_ADDRESS, s_tokenizedPrint3.Length);

        var inputState = new ScriptedHostInputState();
        var inputHandler = new Apple2InputHandler(apple2, NullLoggerFactory.Instance);
        inputHandler.Init(inputState);

        foreach (var key in new[] { HostKey.KeyR, HostKey.KeyU, HostKey.KeyN, HostKey.Enter })
            TypeKey(apple2, inputHandler, inputState, key, shift: false);
        for (var frame = 0; frame < 30; frame++)
            apple2.ExecuteOneFrame();

        var screen = ReadScreen(apple2);
        var runRow = Array.FindIndex(screen, row => row.TrimEnd() == "]RUN");
        Assert.True(runRow >= 0, "RUN was not echoed to the screen.");
        Assert.Equal("3", screen[runRow + 1].TrimEnd());

        foreach (var key in new[] { HostKey.KeyL, HostKey.KeyI, HostKey.KeyS, HostKey.KeyT, HostKey.Enter })
            TypeKey(apple2, inputHandler, inputState, key, shift: false);
        for (var frame = 0; frame < 30; frame++)
            apple2.ExecuteOneFrame();

        screen = ReadScreen(apple2);
        var listRow = Array.FindIndex(screen, row => row.TrimEnd() == "]LIST");
        Assert.True(listRow >= 0, "LIST was not echoed to the screen.");

        // Applesoft's LIST starts with a carriage return, so the listing is not on the row
        // immediately below the echoed command — unlike ordinary output such as PRINT. Find the
        // listing rather than assuming a fixed offset. Applesoft renders it "10  PRINT 3", with
        // two spaces, so match on the parts rather than the exact spacing.
        var listing = screen.Skip(listRow + 1)
            .Select(row => row.TrimEnd())
            .FirstOrDefault(row => row.Length > 0 && row != "]");
        Assert.NotNull(listing);
        Assert.Contains("10", listing);
        Assert.Contains("PRINT 3", listing);
    }

    [RequiresApple2RomFact]
    public void Typed_Input_Reaches_Applesoft_And_Is_Evaluated()
    {
        var apple2 = BootRealRom();

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

    [RequiresApple2RomAndCharacterRomFact]
    public void The_Rasterizer_Draws_The_Banner_From_The_Real_Character_Generator()
    {
        var apple2 = BootRealRom();
        Assert.NotNull(apple2.CharacterRom);

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
    /// Boots the machine on a real ROM. Callers are guarded by
    /// <see cref="RequiresApple2RomFactAttribute"/>, so a missing ROM is a skipped test rather
    /// than something to handle here.
    /// </summary>
    private Apple2System BootRealRom(int runFrames = BootFrames)
    {
        var romPath = Apple2TestRoms.ResolveSystemRomPath();
        Assert.NotNull(romPath);

        _output.WriteLine($"Using Apple II ROM: {romPath}");

        var romData = new Dictionary<string, byte[]>
        {
            { Apple2SystemConfig.SYSTEM_ROM_NAME, File.ReadAllBytes(romPath) },
        };

        var characterRomPath = Apple2TestRoms.ResolveCharacterRomPath();
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
