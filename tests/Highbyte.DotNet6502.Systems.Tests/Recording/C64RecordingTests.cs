using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.IEC;
using Highbyte.DotNet6502.Systems.Recording;
using Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Snapshots;
using static Highbyte.DotNet6502.Systems.Tests.Commodore64.C64StateTestMachine;

namespace Highbyte.DotNet6502.Systems.Tests.Recording;

public class C64RecordingTests
{
    [Fact]
    public void Playback_delivers_the_recorded_restore_edge_once_per_input_frame()
    {
        var source = Build();
        source.Cia1.Keyboard.SetKeysPressed([C64Key.A], restorePressed: true, capsLockOn: false);
        source.Cia1.Joystick.SetJoystickActions(1, [JoystickAction.Up, JoystickAction.Fire]);
        source.Cia1.Joystick.SetJoystickActions(2, [JoystickAction.Left, JoystickAction.Down]);
        source.CPU.CPUInterrupts.ClearPendingNMI();

        // V1 input bytes: key A, both joystick port masks, RESTORE held + edge.
        byte[] frame = [2, 0, 0, 0, 0, 0, 0, 0, 0x11, 0x06, 0x09];
        Assert.Equal(frame, source.CreateRecordingAdapter().CaptureInput());
        var target = Build();
        RestoreInto(source, target);
        var adapter = target.CreateRecordingAdapter();
        Assert.Equal(frame, adapter.CaptureInput());
        Assert.False(target.CPU.CPUInterrupts.NMIPending);
        Assert.False(adapter.CanRepeatInput(frame));
        adapter.ApplyInput(frame);
        Assert.True(target.CPU.CPUInterrupts.NMIPending);
    }

    [Fact]
    public void Checkpoints_include_the_iec_clock_and_active_vic_bank()
    {
        var source = Build();
        var target = Build();
        SwitchVicBankAndHoldIecClock(source);
        RestoreInto(source, target);
        var adapter = target.CreateRecordingAdapter();
        var initial = adapter.ComputeStateDigest();
        Assert.Equal(source.CreateRecordingAdapter().ComputeStateDigest(), initial);
        source.ExecuteOneInstruction(out _);
        target.ExecuteOneInstruction(out _);
        Assert.Equal(source.CreateRecordingAdapter().ComputeStateDigest(), adapter.ComputeStateDigest());

        // Vary each externally visible signal independently of the backing IO bytes.
        RestoreInto(source, target);
        initial = adapter.ComputeStateDigest();
        target.Vic2.SetVIC2Bank(3);
        Assert.NotEqual(initial, adapter.ComputeStateDigest());
        RestoreInto(source, target);
        target.IECBus.Host.SetLines(setCLKLine: DeviceLineState.NotHolding);
        Assert.NotEqual(initial, adapter.ComputeStateDigest());
    }

    [Fact]
    public void Stepping_into_line_zero_is_not_a_completed_frame_even_after_restore()
    {
        var source = Build();
        source.CreateRecordingAdapter().ValidateSession();
        source.ExecuteOneInstruction(out _);
        Assert.Equal(0, source.Vic2.CurrentRasterLine);
        var target = Build();
        RestoreInto(source, target);
        Assert.Throws<InvalidOperationException>(source.CreateRecordingAdapter().ValidateSession);
        Assert.Throws<InvalidOperationException>(target.CreateRecordingAdapter().ValidateSession);
        target.ExecuteOneFrame();
        target.CreateRecordingAdapter().ValidateSession();
        RestoreInto(target, source);
        source.CreateRecordingAdapter().ValidateSession();
    }

    [Theory]
    [InlineData(SidEmulationMode.Auto)]
    [InlineData(SidEmulationMode.Fast)]
    public void Muted_and_sample_output_preserve_the_same_chip_state(SidEmulationMode mode)
    {
        var muted = Build(mode: mode);
        var audible = Build(audio: true, mode: mode);
        int samples = 0;
        audible.SidEmulation.Init(buffer => { samples += buffer.Length; return buffer.Length; });
        // Change sync, ring, noise, TEST, gate and ADSR rates between uneven read intervals.
        var random = new Random(6510);
        for (int interval = 0; interval < 80; interval++)
        {
            for (int register = 0; register < 21; register++)
            {
                ushort address = (ushort)(0xd400 + register);
                byte value = (byte)random.Next(256);
                muted.Mem[address] = value;
                audible.Mem[address] = value;
            }
            for (int i = random.Next(50, 400); i > 0; i--)
            {
                muted.ExecuteOneInstruction(out _);
                audible.ExecuteOneInstruction(out _);
                Assert.Equal(audible.CPU.A, muted.CPU.A);
            }
            Assert.Equal(audible.Mem[0xd41b], muted.Mem[0xd41b]);
            Assert.Equal(audible.Mem[0xd41c], muted.Mem[0xd41c]);
            Assert.Equal(audible.CreateRecordingAdapter().ComputeStateDigest(),
                muted.CreateRecordingAdapter().ComputeStateDigest());
        }
        Assert.True(samples > 0);
    }

    [Fact]
    public void Different_collision_timing_is_an_incompatible_execution_configuration()
    {
        var source = Build();
        var target = Build();
        target.Vic2.SpriteManager.PerLineCollisionEnabled = !source.Vic2.SpriteManager.PerLineCollisionEnabled;
        Assert.NotEqual(((IRecordingSystem)source).CreateRecordingAdapter().CompatibilityId,
            ((IRecordingSystem)target).CreateRecordingAdapter().CompatibilityId);
    }

    [Fact]
    public void Debugger_stop_inside_a_frame_cannot_start_a_recording()
    {
        var source = Build();
        while (source.Vic2.CurrentRasterLine is 0 or ushort.MaxValue)
            source.ExecuteOneInstruction(out _);
        var error = Assert.Throws<InvalidOperationException>(() =>
            ((IRecordingSystem)source).CreateRecordingAdapter().ValidateSession());
        Assert.Contains("frame boundary", error.Message);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Recording_replays_from_midgame_with_different_audio_output(bool sourceAudio, bool targetAudio)
    {
        var source = Build(sourceAudio);
        for (int i = 0; i < 4; i++)
            source.ExecuteOneFrame();
        using var snapshot = new MemoryStream();
        new SnapshotService().Save(source, snapshot);
        using var file = new MemoryStream();
        var sourceAdapter = ((IRecordingSystem)source).CreateRecordingAdapter();
        using var recording = new RecordingSession(sourceAdapter);
        recording.StartRecording(file, snapshot);
        var expectedStates = new List<(ushort Pc, ulong Cycles, byte[] Ram, byte Osc)>();
        for (int i = 0; i < 18; i++)
        {
            source.Cia1.Keyboard.SetKeysPressed(i < 9 ? [C64Key.A] : [], false, false);
            source.Cia1.Joystick.CurrentJoystickActions[2].Clear();
            if (i < 9)
                source.Cia1.Joystick.CurrentJoystickActions[2].Add(Highbyte.DotNet6502.Systems.Input.JoystickAction.Right);
            recording.BeforeFrame();
            source.ExecuteOneFrame();
            expectedStates.Add((source.CPU.PC, source.CPU.ExecState.CyclesConsumed, source.RAM.ToArray(), source.SidEmulation.Core.Osc3));
            recording.AfterFrame();
        }
        recording.StopRecording();
        file.Position = 0;
        using var reader = new DemoReader(file);
        var target = Build(targetAudio);
        target.Cia1.Joystick.KeyboardJoystickEnabled = true;
        using (var initial = reader.OpenSnapshot())
            new SnapshotService().Restore(target, initial);
        var targetAdapter = ((IRecordingSystem)target).CreateRecordingAdapter();
        using var playback = new RecordingSession(targetAdapter);
        playback.StartPlayback(reader);
        while (playback.Mode == RecordingMode.Playback && playback.BeforeFrame())
        {
            target.ExecuteOneFrame();
            var expected = expectedStates[(int)playback.Frame];
            Assert.Equal(expected.Pc, target.CPU.PC);
            Assert.Equal(expected.Cycles, target.CPU.ExecState.CyclesConsumed);
            Assert.Equal(expected.Ram, target.RAM);
            Assert.Equal(expected.Osc, target.SidEmulation.Core.Osc3);
            playback.AfterFrame();
        }
        Assert.Equal(PlaybackOutcome.Completed, playback.Outcome);
        Assert.Equal(source.RAM, target.RAM);
        Assert.Equal(source.CPU.PC, target.CPU.PC);
    }
}
