using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Snapshots;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

internal static class C64StateTestMachine
{
    internal static void RestoreInto(C64 source, C64 target)
    {
        using var snapshot = new MemoryStream();
        var service = new SnapshotService();
        service.Save(source, snapshot);
        snapshot.Position = 0;
        service.Restore(target, snapshot);
    }

    internal static void SwitchVicBankAndHoldIecClock(C64 source)
    {
        source.Mem[0xdd02] = 3;
        source.Mem[0xdd00] = 3;
        source.CPU.A = 0x12; // Hold IEC clock low and queue VIC bank 1 on the final STA bus cycle.
        source.Mem.StoreData(0xc000, [0x8d, 0x00, 0xdd, 0xea]);
        source.ExecuteOneInstruction(out _);
    }

    internal static C64 Build(bool audio = false, SidEmulationMode mode = SidEmulationMode.Auto)
    {
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false, C64Model = "C64PAL", Vic2Model = "PAL", AudioEnabled = audio,
            AudioProviderType = typeof(C64SidSampleProvider),
            SidEmulationMode = mode,
        }, NullLoggerFactory.Instance);
        // Observe joystick pins and SID oscillator readback, with no copyrighted ROM fixtures.
        c64.Mem.StoreData(0xC000, new byte[]
        {
            0xAD, 0x00, 0xDC, 0x8D, 0x00, 0x40,
            0xAD, 0x1B, 0xD4, 0x8D, 0x01, 0x40,
            0xEE, 0x02, 0x40, 0x4C, 0x00, 0xC0,
        });
        c64.CPU.PC = 0xC000;
        c64.Mem[0xD40E] = 0x73;
        c64.Mem[0xD40F] = 0x37;
        c64.Mem[0xD412] = 0x21;
        // Expanded sprite DMA spans the frame boundary; losing its sequencer changes CPU stalls.
        c64.Mem[0xD001] = 250;
        c64.Mem[0xD015] = 1;
        c64.Mem[0xD017] = 1;
        return c64;
    }
}
