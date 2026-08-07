using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Input;
using Highbyte.DotNet6502.Systems.Apple2.Render;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;
using Highbyte.DotNet6502.Systems.Tests.Apple2.TestRom;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// End-to-end verification of the Apple II machine against a home-made ROM image
/// (<see cref="Apple2SyntheticRom"/>) — no third-party ROM needed, so this runs in CI.
/// </summary>
public class Apple2SyntheticRomTests
{
    private static Apple2System BootSyntheticRom()
    {
        var romData = new Dictionary<string, byte[]>
        {
            { Apple2SystemConfig.SYSTEM_ROM_NAME, Apple2SyntheticRom.Build() },
        };
        // Pinned to a phosphor monitor so the colour assertions below stay independent of
        // whichever monitor the shipped config defaults to.
        return new Apple2System(
            new Apple2Config { MonitorColor = Apple2MonitorColor.Green },
            NullLoggerFactory.Instance,
            romData);
    }

    /// <summary>The rasterizer is the default provider, so select the command stream explicitly.</summary>
    private static Apple2VideoCommandStream GetCommandStream(Apple2System apple2)
    {
        apple2.SetCurrentRenderProviderType(typeof(Apple2VideoCommandStream));
        return (Apple2VideoCommandStream)apple2.RenderProvider!;
    }

    private static List<DrawGlyph> LastFrameGlyphs(Apple2System apple2)
        => GetCommandStream(apple2).DequeueAll().OfType<DrawGlyph>().ToList();

    /// <summary>Discards any queued commands and returns the glyphs of one freshly run frame.</summary>
    private static List<DrawGlyph> RenderFreshFrame(Apple2System apple2)
    {
        var stream = GetCommandStream(apple2);
        _ = stream.DequeueAll();
        apple2.ExecuteOneFrame();
        return stream.DequeueAll().OfType<DrawGlyph>().ToList();
    }

    [Fact]
    public void Reset_Vector_Starts_Execution_In_The_Rom()
    {
        var apple2 = BootSyntheticRom();

        Assert.Equal(Apple2SyntheticRom.ProgramStartAddress, apple2.CPU.PC);
    }

    [Fact]
    public void One_Frame_Clears_The_Screen_And_Writes_The_Row_Banner()
    {
        var apple2 = BootSyntheticRom();

        apple2.ExecuteOneFrame();

        for (var row = 0; row < Apple2TextScreen.Rows; row++)
        {
            var expected = (byte)(Apple2SyntheticRom.BannerFirstScreenByte + row);
            Assert.Equal(expected, apple2.Mem[Apple2TextScreen.GetAddress(row, 0)]);

            // Everything the clear loop touched is a normal-video space.
            Assert.Equal((byte)0xA0, apple2.Mem[Apple2TextScreen.GetAddress(row, 1)]);
            Assert.Equal((byte)0xA0, apple2.Mem[Apple2TextScreen.GetAddress(row, 39)]);
        }
    }

    [Fact]
    public void The_Row_Banner_Is_Rendered_On_The_Expected_Screen_Rows()
    {
        var apple2 = BootSyntheticRom();

        // Runs the boot frame with the command stream selected, so its output is captured.
        var glyphs = RenderFreshFrame(apple2);

        for (var row = 0; row < Apple2TextScreen.Rows; row++)
        {
            var glyph = glyphs.Single(g => g.X == 0 && g.Y == row);
            Assert.Equal(Apple2SyntheticRom.BannerFirstScreenByte + row, glyph.GlyphId);
            // 'A' at row 0 through 'X' at row 23 — the interleaved addresses the ROM's own
            // row-base table produced land on the right display rows.
            Assert.Equal((char)('A' + row), Apple2CharSet.ScreenCodeToUnicode((byte)glyph.GlyphId).Single());
        }
    }

    [Fact]
    public void Typed_Characters_Are_Echoed_To_The_Screen()
    {
        var apple2 = BootSyntheticRom();
        var inputState = new ScriptedHostInputState();
        var inputHandler = new Apple2InputHandler(apple2, NullLoggerFactory.Instance);
        inputHandler.Init(inputState);

        apple2.ExecuteOneFrame();   // boot: clear + banner, then the keyboard poll loop

        Type(apple2, inputHandler, inputState, HostKey.KeyH);

        var address = Apple2TextScreen.GetAddress(Apple2SyntheticRom.EchoRow, Apple2SyntheticRom.EchoFirstColumn);
        Assert.Equal("H", Apple2CharSet.ScreenCodeToUnicode(apple2.Mem[address]));
        Assert.False(apple2.Keyboard.StrobeSet);   // the ROM cleared the strobe via $C010
    }

    [Fact]
    public void Echoed_Characters_Cycle_Through_Normal_Inverse_And_Flashing_Video()
    {
        var apple2 = BootSyntheticRom();
        var inputState = new ScriptedHostInputState();
        var inputHandler = new Apple2InputHandler(apple2, NullLoggerFactory.Instance);
        inputHandler.Init(inputState);

        apple2.ExecuteOneFrame();

        foreach (var key in new[] { HostKey.KeyA, HostKey.KeyB, HostKey.KeyC })
            Type(apple2, inputHandler, inputState, key);

        var column = Apple2SyntheticRom.EchoFirstColumn;
        var first = apple2.Mem[Apple2TextScreen.GetAddress(Apple2SyntheticRom.EchoRow, column)];
        var second = apple2.Mem[Apple2TextScreen.GetAddress(Apple2SyntheticRom.EchoRow, column + 1)];
        var third = apple2.Mem[Apple2TextScreen.GetAddress(Apple2SyntheticRom.EchoRow, column + 2)];

        Assert.Equal("A", Apple2CharSet.ScreenCodeToUnicode(first));
        Assert.Equal("B", Apple2CharSet.ScreenCodeToUnicode(second));
        Assert.Equal("C", Apple2CharSet.ScreenCodeToUnicode(third));

        Assert.Equal(Apple2TextAttribute.Normal, Apple2CharSet.GetAttribute(first));
        Assert.Equal(Apple2TextAttribute.Inverse, Apple2CharSet.GetAttribute(second));
        Assert.Equal(Apple2TextAttribute.Flash, Apple2CharSet.GetAttribute(third));
    }

    [Fact]
    public void Inverse_And_Flashing_Echoes_Reach_The_Render_Output()
    {
        var apple2 = BootSyntheticRom();
        var inputState = new ScriptedHostInputState();
        var inputHandler = new Apple2InputHandler(apple2, NullLoggerFactory.Instance);
        inputHandler.Init(inputState);

        apple2.ExecuteOneFrame();
        foreach (var key in new[] { HostKey.KeyA, HostKey.KeyB })
            Type(apple2, inputHandler, inputState, key);

        var glyphs = RenderFreshFrame(apple2);
        var normal = glyphs.Single(g => g.X == Apple2SyntheticRom.EchoFirstColumn && g.Y == Apple2SyntheticRom.EchoRow);
        var inverse = glyphs.Single(g => g.X == Apple2SyntheticRom.EchoFirstColumn + 1 && g.Y == Apple2SyntheticRom.EchoRow);

        Assert.Equal(Apple2Colors.GetForeground(Apple2MonitorColor.Green), normal.ForeColor);
        Assert.Equal(Apple2Colors.Background, normal.BackColor);

        Assert.Equal(Apple2Colors.Background, inverse.ForeColor);
        Assert.Equal(Apple2Colors.GetForeground(Apple2MonitorColor.Green), inverse.BackColor);
    }

    [Fact]
    public void ExtractSystemRomImage_Accepts_A_Trimmed_Twelve_Kilobyte_Image()
    {
        var image = Apple2SyntheticRom.Build();

        Assert.Same(image, Apple2System.ExtractSystemRomImage(image));
    }

    [Fact]
    public void ExtractSystemRomImage_Takes_The_Last_Twelve_Kilobytes_Of_A_Larger_Layout()
    {
        // The 20,480-byte $B000-$FFFF layout some emulator distributions ship.
        var trimmed = Apple2SyntheticRom.Build();
        var full = new byte[20480];
        Array.Fill(full, (byte)0x5A);
        trimmed.CopyTo(full, full.Length - trimmed.Length);

        Assert.Equal(trimmed, Apple2System.ExtractSystemRomImage(full));
    }

    [Fact]
    public void ExtractSystemRomImage_Rejects_An_Undersized_Image()
    {
        Assert.Throws<DotNet6502Exception>(() => Apple2System.ExtractSystemRomImage(new byte[1024]));
    }

    [Fact]
    public void A_Larger_Rom_Layout_Boots_The_Same_Way()
    {
        var trimmed = Apple2SyntheticRom.Build();
        var full = new byte[20480];
        trimmed.CopyTo(full, full.Length - trimmed.Length);

        var apple2 = new Apple2System(
            new Apple2Config(),
            NullLoggerFactory.Instance,
            new Dictionary<string, byte[]> { { Apple2SystemConfig.SYSTEM_ROM_NAME, full } });

        apple2.ExecuteOneFrame();

        Assert.Equal(Apple2SyntheticRom.BannerFirstScreenByte, apple2.Mem[Apple2TextScreen.GetAddress(0, 0)]);
    }

    private static void Type(
        Apple2System apple2,
        Apple2InputHandler inputHandler,
        ScriptedHostInputState inputState,
        HostKey key)
    {
        inputState.KeysDown = new HashSet<HostKey> { key };
        inputHandler.BeforeFrame();
        apple2.ExecuteOneFrame();

        // Release, so the next press registers as a new edge rather than an auto-repeat.
        inputState.KeysDown = new HashSet<HostKey>();
        inputHandler.BeforeFrame();
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
