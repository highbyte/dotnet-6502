using Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Snapshots;
using static Highbyte.DotNet6502.Systems.Tests.Commodore64.C64StateTestMachine;

namespace Highbyte.DotNet6502.Systems.Tests.Snapshots;

public class C64ExecutionSnapshotTests
{
    [Fact]
    public void Snapshot_restores_held_controls_without_delivering_another_restore_edge()
    {
        var source = Build();
        source.Cia1.Keyboard.SetKeysPressed([C64Key.A], restorePressed: true, capsLockOn: false);
        source.Cia1.Joystick.SetJoystickActions(1, [JoystickAction.Up, JoystickAction.Fire]);
        source.Cia1.Joystick.SetJoystickActions(2, [JoystickAction.Left, JoystickAction.Down]);
        source.CPU.CPUInterrupts.ClearPendingNMI();

        var target = Build();
        RestoreInto(source, target);
        Assert.True(target.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.A));
        Assert.True(target.Cia1.Joystick.CurrentJoystickActions[1].SetEquals([JoystickAction.Up, JoystickAction.Fire]));
        Assert.True(target.Cia1.Joystick.CurrentJoystickActions[2].SetEquals([JoystickAction.Left, JoystickAction.Down]));
        Assert.False(target.CPU.CPUInterrupts.NMIPending);
    }

    [Fact]
    public void Released_nmi_pulse_survives_until_the_cpu_services_it()
    {
        var source = Build();
        var target = Build();
        source.CPU.CPUInterrupts.SetNMISourceActive("pulse");
        source.CPU.CPUInterrupts.SetNMISourceInactive("pulse");
        RestoreInto(source, target);
        Assert.True(target.CPU.CPUInterrupts.NMIPending);
        Assert.Empty(target.CPU.CPUInterrupts.ActiveNMISources);
        source.ExecuteOneInstruction(out _);
        target.ExecuteOneInstruction(out _);
        Assert.False(target.CPU.CPUInterrupts.NMIPending);
        Assert.Equal(source.CPU.PC, target.CPU.PC);
    }

    [Fact]
    public void Snapshot_preserves_iec_clock_and_applies_the_pending_vic_bank_switch()
    {
        var source = Build();
        var target = Build();
        SwitchVicBankAndHoldIecClock(source);
        RestoreInto(source, target);
        Assert.Equal(0, source.Vic2.CurrentVIC2Bank);
        Assert.Equal(source.Vic2.CurrentVIC2Bank, target.Vic2.CurrentVIC2Bank);
        Assert.Equal(0, source.Mem[0xdd00] & 0x40);
        Assert.Equal(source.Mem[0xdd00], target.Mem[0xdd00]);
        source.ExecuteOneInstruction(out _);
        target.ExecuteOneInstruction(out _);
        Assert.Equal(1, target.Vic2.CurrentVIC2Bank);
    }

    [Theory]
    [InlineData(SidEmulationMode.Fast, SidEmulationMode.Auto)]
    [InlineData(SidEmulationMode.Auto, SidEmulationMode.Fast)]
    public void Snapshot_resumes_saved_sid_readback_mode(SidEmulationMode from, SidEmulationMode to)
    {
        var source = Build(mode: from);
        source.ExecuteOneFrame();
        var target = Build(mode: to);
        RestoreInto(source, target);
        Assert.Equal(from, target.SidEmulation.Core.Mode);
        for (int i = 0; i < 20; i++)
        {
            source.ExecuteOneInstruction(out _);
            target.ExecuteOneInstruction(out _);
            Assert.Equal(source.Mem[0xd41b], target.Mem[0xd41b]);
        }
    }

    [Fact]
    public void Snapshot_preserves_collision_latches_and_irq_suppression()
    {
        var source = Build();
        source.ExecuteOneFrame();
        var sprites = source.Vic2.SpriteManager;
        sprites.SpriteToSpriteCollisionStore = 3;
        sprites.SpriteToBackgroundCollisionStore = 5;
        sprites.SpriteToSpriteCollisionIRQBlock = true;
        sprites.SpriteToBackgroundCollisionIRQBlock = true;
        using var snapshot = new MemoryStream();
        new SnapshotService().Save(source, snapshot);
        snapshot.Position = 0;
        var target = Build();
        new SnapshotService().Restore(target, snapshot);
        Assert.Equal(sprites.SpriteToSpriteCollisionStore, target.Vic2.SpriteManager.SpriteToSpriteCollisionStore);
        Assert.Equal(sprites.SpriteToBackgroundCollisionStore, target.Vic2.SpriteManager.SpriteToBackgroundCollisionStore);
        Assert.True(target.Vic2.SpriteManager.SpriteToSpriteCollisionIRQBlock);
        Assert.True(target.Vic2.SpriteManager.SpriteToBackgroundCollisionIRQBlock);
        Assert.Equal(source.Mem[0xD01E], target.Mem[0xD01E]);
        Assert.Equal(source.Mem[0xD01F], target.Mem[0xD01F]);
    }

    [Theory]
    [InlineData(0xDC0D)]
    [InlineData(0xDD0D)]
    public void Snapshot_preserves_pending_cia_interrupt_edges(int interruptRegister)
    {
        var source = Build();
        source.Mem[0xFFFA] = 0x00;
        source.Mem[0xFFFB] = 0xC0;
        source.Mem[0xFFFE] = 0x00;
        source.Mem[0xFFFF] = 0xC0;
        source.Mem[(ushort)interruptRegister] = 0x81;
        source.Mem[(ushort)(interruptRegister - 9)] = 10;
        source.Mem[(ushort)(interruptRegister - 8)] = 0;
        source.Mem[(ushort)(interruptRegister + 1)] = 0x11;
        for (int i = 0; i < 8; i++) source.ExecuteOneInstruction(out _);
        using var snapshot = new MemoryStream();
        new SnapshotService().Save(source, snapshot);
        snapshot.Position = 0;
        var target = Build();
        new SnapshotService().Restore(target, snapshot);
        Assert.Equal(source.CPU.CaptureExecutionBoundaryState(), target.CPU.CaptureExecutionBoundaryState());
        Assert.Equal(source.Mem[(ushort)interruptRegister], target.Mem[(ushort)interruptRegister]);
        for (int i = 0; i < 100; i++)
        {
            source.ExecuteOneInstruction(out _);
            target.ExecuteOneInstruction(out _);
            Assert.Equal(source.CPU.PC, target.CPU.PC);
            Assert.Equal(source.CPU.CaptureExecutionBoundaryState(), target.CPU.CaptureExecutionBoundaryState());
        }
    }

    [Fact]
    public void Snapshot_restores_execution_modules_exactly()
    {
        var source = Build();
        for (int i = 0; i < 4; i++) source.ExecuteOneFrame();
        using var initial = new MemoryStream();
        new SnapshotService().Save(source, initial);
        initial.Position = 0;
        var target = Build();
        new SnapshotService().Restore(target, initial);
        var ramDifference = Enumerable.Range(0, source.RAM.Length).FirstOrDefault(i => source.RAM[i] != target.RAM[i], -1);
        var ioDifference = Enumerable.Range(0, source.IO.Length).FirstOrDefault(i => source.IO[i] != target.IO[i], -1);
        Assert.True(ramDifference == -1, $"RAM differs at ${ramDifference:X4}");
        Assert.True(ioDifference == -1, $"IO differs at ${ioDifference + 0xD000:X4}: {source.IO[Math.Max(0, ioDifference)]} -> {target.IO[Math.Max(0, ioDifference)]}");
        using var restored = new MemoryStream();
        new SnapshotService().Save(target, restored);
        initial.Position = 0;
        restored.Position = 0;
        using var sourceZip = new System.IO.Compression.ZipArchive(initial);
        using var targetZip = new System.IO.Compression.ZipArchive(restored);
        foreach (var entry in sourceZip.Entries.Where(e => e.FullName.StartsWith("modules/", StringComparison.Ordinal)))
        {
            using var before = new MemoryStream();
            using var after = new MemoryStream();
            using (var s = entry.Open()) s.CopyTo(before);
            using (var s = targetZip.GetEntry(entry.FullName)!.Open()) s.CopyTo(after);
            Assert.True(before.ToArray().SequenceEqual(after.ToArray()), $"Snapshot module differs: {entry.FullName}");
        }
    }

    [Fact]
    public void Snapshot_resumes_sid_observing_program_instruction_for_instruction()
    {
        var source = Build();
        for (int i = 0; i < 4; i++) source.ExecuteOneFrame();
        using var snapshot = new MemoryStream();
        new SnapshotService().Save(source, snapshot);
        snapshot.Position = 0;
        var target = Build();
        new SnapshotService().Restore(target, snapshot);
        for (int i = 0; i < 500; i++)
        {
            source.ExecuteOneInstruction(out _);
            target.ExecuteOneInstruction(out _);
            Assert.True(source.SidEmulation.Core.Osc3 == target.SidEmulation.Core.Osc3,
                $"SID diverged after instruction {i} at ${source.CPU.PC:X4}: {source.SidEmulation.Core.Osc3} vs {target.SidEmulation.Core.Osc3}; cycles {source.CPU.BusCycles} vs {target.CPU.BusCycles}");
            Assert.Equal(source.CPU.A, target.CPU.A);
        }
    }

    [Fact]
    public void Snapshot_preserves_sid_readbacks_and_restore_edge_history()
    {
        var source = Build();
        source.Cia1.Keyboard.SetKeysPressed([C64Key.Stop], restorePressed: true, capsLockOn: true);
        source.ExecuteOneFrame();
        using var snapshot = new MemoryStream();
        new SnapshotService().Save(source, snapshot);
        var target = Build();
        snapshot.Position = 0;
        new SnapshotService().Restore(target, snapshot);
        Assert.Equal(source.SidEmulation.Core.Osc3, target.SidEmulation.Core.Osc3);
        Assert.Equal(source.SidEmulation.Core.Env3, target.SidEmulation.Core.Env3);
        Assert.True(target.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.Stop));
        Assert.True(target.Cia1.Keyboard.IsKeyCurrentlyPressed(C64Key.LShift));
        source.Cia1.Keyboard.SetKeysPressed([C64Key.Stop], true, true);
        target.Cia1.Keyboard.SetKeysPressed([C64Key.Stop], true, true);
        Assert.Equal(source.CPU.CPUInterrupts.NMIPending, target.CPU.CPUInterrupts.NMIPending);
    }
}
