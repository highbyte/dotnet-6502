using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Peripherals;
using Highbyte.DotNet6502.Systems.Apple2.Snapshots;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Snapshots;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Snapshots;

public class Apple2SnapshotRoundTripTests
{
    // Program in RAM that writes a marker and advances X/Y in a loop, so continued execution after
    // restore is observable:
    //   0300: A9 5A      LDA #$5A
    //   0302: 8D 00 04   STA $0400   (text page 1)
    //   0305: E8         INX
    //   0306: C8         INY
    //   0307: 4C 05 03   JMP $0305
    private const ushort ProgramStart = 0x0300;
    private static readonly byte[] Program =
    {
        0xA9, 0x5A,
        0x8D, 0x00, 0x04,
        0xE8,
        0xC8,
        0x4C, 0x05, 0x03,
    };

    private static Apple2System BuildApple2()
        => new Apple2System(new Apple2Config(), NullLoggerFactory.Instance);

    private static void LoadProgram(Apple2System apple2)
    {
        for (var i = 0; i < Program.Length; i++)
            apple2.Mem.Write((ushort)(ProgramStart + i), Program[i]);
        apple2.CPU.PC = ProgramStart;
    }

    [Fact]
    public void Apple2_implements_snapshot_provider_with_cpu_core_and_disk_modules()
    {
        var provider = (ISystemSnapshotProvider)BuildApple2();

        Assert.Equal(Apple2System.SystemName, provider.MachineId.SystemName);
        Assert.Equal(Apple2System.SnapshotVersion, provider.MachineId.SupportedSnapshotVersion);

        var moduleNames = provider.GetSnapshotModules().Select(m => m.Name).ToArray();
        Assert.Equal(
            new[]
            {
                Cpu6502SnapshotModule.ModuleName,
                Apple2CoreSnapshotModule.ModuleName,
                Apple2LanguageCardSnapshotModule.ModuleName,
                Apple2Disk2SnapshotModule.ModuleName,
            },
            moduleNames);
    }

    /// <summary>
    /// The card holds ProDOS itself once a ProDOS disk has booted, so losing it on restore would
    /// replace the machine's operating system with zeros. The switch state travels too: it decides
    /// whether the CPU resumes executing from the card or from ROM.
    /// </summary>
    [Fact]
    public void Round_trip_restores_language_card_contents_and_switch_state()
    {
        var source = BuildApple2();

        // Read+write the card, bank 1, and leave recognisable bytes in both of its regions.
        source.Mem.Read(0xC08B);
        source.Mem.Read(0xC08B);
        source.Mem.Write(0xD000, 0x11);
        source.Mem.Write(0xE000, 0x22);

        // Then switch to bank 2 and write something different at the same address.
        source.Mem.Read(0xC083);
        source.Mem.Read(0xC083);
        source.Mem.Write(0xD000, 0x33);

        Assert.True(source.LanguageCard.ReadRam);
        Assert.True(source.LanguageCard.WriteEnabled);
        Assert.False(source.LanguageCard.Bank1Selected);

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildApple2();
        // A fresh machine powers up reading ROM with the card protected — the opposite of the
        // captured state, so a module that skipped this would fail rather than pass by accident.
        Assert.False(restored.LanguageCard.ReadRam);
        new SnapshotService().Restore(restored, snapshotStream);

        Assert.True(restored.LanguageCard.ReadRam);
        Assert.True(restored.LanguageCard.WriteEnabled);
        Assert.False(restored.LanguageCard.Bank1Selected);

        // The restored machine must also be *reading* the card, not just recording that it should:
        // the memory configuration has to have been applied.
        Assert.Equal((byte)0x33, restored.Mem.Read(0xD000));
        Assert.Equal((byte)0x22, restored.Mem.Read(0xE000));

        // The bank that was not selected at capture time survives too.
        restored.Mem.Read(0xC08B);
        restored.Mem.Read(0xC08B);
        Assert.Equal((byte)0x11, restored.Mem.Read(0xD000));
    }

    [Fact]
    public void Round_trip_restores_memory_registers_and_resumes_execution()
    {
        var source = BuildApple2();
        LoadProgram(source);
        for (var i = 0; i < 20; i++)
            source.ExecuteOneInstruction(out _);

        Assert.Equal((byte)0x5A, source.Mem.Read(0x0400));

        using var snapshotStream = new MemoryStream();
        var service = new SnapshotService();
        service.Save(source, snapshotStream);

        // Restore into a fresh machine that has been perturbed away from the captured state.
        snapshotStream.Position = 0;
        var restored = BuildApple2();
        restored.CPU.PC = 0x0000;
        restored.Mem.Write(0x0400, 0x00);
        var result = service.Restore(restored, snapshotStream);

        Assert.Empty(result.Warnings);
        Assert.Equal((byte)0x5A, restored.Mem.Read(0x0400));
        Assert.Equal(Program[0], restored.Mem.Read(ProgramStart));
        Assert.Equal(source.CPU.PC, restored.CPU.PC);
        Assert.Equal(source.CPU.X, restored.CPU.X);
        Assert.Equal(source.CPU.Y, restored.CPU.Y);
        Assert.Equal(source.CPU.A, restored.CPU.A);

        // Continued execution mirrors the source machine.
        for (var i = 0; i < 30; i++)
        {
            source.ExecuteOneInstruction(out _);
            restored.ExecuteOneInstruction(out _);
        }
        Assert.Equal(source.CPU.PC, restored.CPU.PC);
        Assert.Equal(source.CPU.X, restored.CPU.X);
        Assert.Equal(source.CPU.Y, restored.CPU.Y);
    }

    [Fact]
    public void Round_trip_restores_display_soft_switches()
    {
        var source = BuildApple2();
        // Hi-res, mixed, page 2, graphics — every switch away from its power-on value.
        source.Mem.Read(Apple2SoftSwitches.GraphicsModeAddress);
        source.Mem.Read(Apple2SoftSwitches.MixedModeOnAddress);
        source.Mem.Read(Apple2SoftSwitches.TextPage2Address);
        source.Mem.Read(Apple2SoftSwitches.HiResModeAddress);

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildApple2();
        // A fresh machine powers up in text mode, page 1 — the opposite of every value above, so a
        // module that silently skipped these would leave the assertions below failing rather than
        // accidentally passing.
        Assert.True(restored.SoftSwitches.TextMode);
        new SnapshotService().Restore(restored, snapshotStream);

        Assert.False(restored.SoftSwitches.TextMode);
        Assert.True(restored.SoftSwitches.MixedMode);
        Assert.True(restored.SoftSwitches.Page2);
        Assert.True(restored.SoftSwitches.HiRes);
        Assert.Equal(source.SoftSwitches.ActiveHiResPageBaseAddress, restored.SoftSwitches.ActiveHiResPageBaseAddress);
    }

    [Fact]
    public void Round_trip_restores_keyboard_latch_including_strobe_state()
    {
        var source = BuildApple2();
        source.Keyboard.KeyPressed((byte)'Q');
        Assert.True(source.Keyboard.StrobeSet);

        using var pending = new MemoryStream();
        new SnapshotService().Save(source, pending);

        // ...and again after the machine has consumed the keypress, so the strobe bit is proven to
        // travel rather than being implied by the ASCII code.
        source.Keyboard.ClearStrobe();
        using var consumed = new MemoryStream();
        new SnapshotService().Save(source, consumed);

        pending.Position = 0;
        var restoredPending = BuildApple2();
        new SnapshotService().Restore(restoredPending, pending);
        Assert.True(restoredPending.Keyboard.StrobeSet);
        Assert.Equal((byte)'Q', (byte)(restoredPending.Keyboard.Latch & 0x7F));

        consumed.Position = 0;
        var restoredConsumed = BuildApple2();
        new SnapshotService().Restore(restoredConsumed, consumed);
        Assert.False(restoredConsumed.Keyboard.StrobeSet);
        Assert.Equal((byte)'Q', restoredConsumed.Keyboard.Latch);
    }

    [Fact]
    public void Round_trip_restores_speaker_level_and_toggle_count()
    {
        var source = BuildApple2();
        LoadProgram(source);
        // An odd number of toggles, so the cone ends up away from its power-on position.
        for (var i = 0; i < 5; i++)
            source.Mem.Read(Apple2SoftSwitches.SpeakerToggleAddress);
        Assert.True(source.Speaker.Level);

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildApple2();
        new SnapshotService().Restore(restored, snapshotStream);

        Assert.Equal(source.Speaker.Level, restored.Speaker.Level);
        Assert.Equal(source.Speaker.ToggleCount, restored.Speaker.ToggleCount);
        Assert.Equal(source.Speaker.LastToggleCycle, restored.Speaker.LastToggleCycle);
    }

    [Fact]
    public void Round_trip_restores_paddle_positions_buttons_and_counters()
    {
        var source = BuildApple2();
        source.GamePort.SetPaddlePosition(0, 200);
        source.GamePort.SetPaddlePosition(1, 40);
        source.GamePort.SetButton(0, true);
        source.GamePort.SetButton(1, false);
        source.Mem.Read(Apple2GamePort.Button0Address);      // bump a read counter
        source.Mem.Read(Apple2SoftSwitches.SpeakerToggleAddress);
        source.Mem.Read(0xC070);                             // strobe the one-shot

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildApple2();
        new SnapshotService().Restore(restored, snapshotStream);

        Assert.Equal((byte)200, restored.GamePort.GetPaddlePosition(0));
        Assert.Equal((byte)40, restored.GamePort.GetPaddlePosition(1));
        Assert.True(restored.GamePort.IsButtonPressed(0));
        Assert.False(restored.GamePort.IsButtonPressed(1));
        Assert.Equal(source.GamePort.PaddleTriggerCount, restored.GamePort.PaddleTriggerCount);
        Assert.Equal(source.GamePort.ButtonReadCounts[0], restored.GamePort.ButtonReadCounts[0]);
    }

    /// <summary>
    /// The cumulative CPU cycle count is restored (see <see cref="Cpu6502SnapshotModule"/>), which is
    /// what makes the machine's absolute cycle stamps survive a round trip. Without it the restored
    /// counter would restart near zero, putting every stamp in the future.
    /// </summary>
    [Fact]
    public void Round_trip_restores_cumulative_cpu_cycle_count()
    {
        var source = BuildApple2();
        LoadProgram(source);
        for (var i = 0; i < 50; i++)
            source.ExecuteOneInstruction(out _);

        var sourceCycles = source.CPU.ExecState.CyclesConsumed;
        Assert.True(sourceCycles > 0);

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildApple2();
        new SnapshotService().Restore(restored, snapshotStream);

        Assert.Equal(sourceCycles, restored.CPU.ExecState.CyclesConsumed);
        Assert.Equal(
            source.CPU.ExecState.InstructionsExecutionCount,
            restored.CPU.ExecState.InstructionsExecutionCount);
    }

    /// <summary>
    /// The payoff of restoring the cycle count: a paddle read caught mid-flight resumes with its
    /// one-shot still running and expiring at the same point, instead of the timer reading as
    /// already expired (or, worse, as freshly triggered) on the restored machine.
    /// </summary>
    [Fact]
    public void Round_trip_resumes_an_in_flight_paddle_one_shot()
    {
        var source = BuildApple2();
        LoadProgram(source);
        // Burn cycles first, so the trigger stamp is a large absolute value rather than one a fresh
        // machine could coincidentally match.
        for (var i = 0; i < 200; i++)
            source.ExecuteOneInstruction(out _);

        source.GamePort.SetPaddlePosition(0, Apple2GamePort.PaddleMax);
        source.Mem.Read(Apple2GamePort.TriggerAddress);
        Assert.True(source.GamePort.IsPaddleTimerRunning(0));

        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var restored = BuildApple2();
        new SnapshotService().Restore(restored, snapshotStream);

        // Still running at the moment of restore...
        Assert.True(restored.GamePort.IsPaddleTimerRunning(0));

        // ...and expires after the same amount of further execution as on the source machine. The
        // loop runs long enough to outlast a full-scale one-shot (PaddleMax * PreadLoopCycles =
        // 2,805 cycles, at roughly 2.6 cycles per instruction in this program).
        for (var i = 0; i < 1500; i++)
        {
            source.ExecuteOneInstruction(out _);
            restored.ExecuteOneInstruction(out _);
            Assert.Equal(source.GamePort.IsPaddleTimerRunning(0), restored.GamePort.IsPaddleTimerRunning(0));
        }
        Assert.False(restored.GamePort.IsPaddleTimerRunning(0));
    }

    [Fact]
    public void Restoring_a_snapshot_of_another_machine_is_rejected()
    {
        var source = BuildApple2();
        using var snapshotStream = new MemoryStream();
        new SnapshotService().Save(source, snapshotStream);

        snapshotStream.Position = 0;
        var genericComputer = new GenericComputer();
        Assert.Throws<SnapshotIncompatibleException>(
            () => new SnapshotService().Restore(genericComputer, snapshotStream));
    }
}
