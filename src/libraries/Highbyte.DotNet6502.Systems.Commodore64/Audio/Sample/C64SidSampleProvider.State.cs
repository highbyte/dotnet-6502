namespace Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;

public sealed partial class C64SidSampleProvider
{
    internal void RestoreState(ulong coreCycles, byte[] state)
    {
        _core.RestoreState(state);
        WireSidRegisters();
        _coreCycles = coreCycles;
        _clockStarted = true;
    }

    internal void RestoreLegacyRegisters()
    {
        for (int offset = 0; offset < SidSampleCore.RegisterCount; offset++)
            _core.WriteRegister(offset, _c64.Sid.InternalSidState.GetRawSidRegValue((ushort)(SidAddr.FRELO1 + offset)));
        _coreCycles = _c64.CPU.BusCycles;
    }
}
