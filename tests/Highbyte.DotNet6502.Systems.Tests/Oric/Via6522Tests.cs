using Highbyte.DotNet6502.Systems.Oric.Hardware;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class Via6522Tests
{
    [Fact]
    public void TimerOneRaisesAndClearsIrq()
    {
        var irq = false;
        var via = new Via6522(irqChanged: active => irq = active);
        via.Write(0x0e, 0xc0); // enable timer-one interrupt
        via.Write(0x04, 0x00);
        via.Write(0x05, 0x00);

        via.ProcessCycles(1);

        Assert.True(irq);
        Assert.Equal(Via6522.InterruptTimer1, via.InterruptFlags & Via6522.InterruptTimer1);
        via.Read(0x04);
        Assert.False(irq);
    }

    [Fact]
    public void DataDirectionCombinesOutputLatchAndInputPins()
    {
        var via = new Via6522(readPortAInput: () => 0x0f);
        via.Write(0x03, 0xf0);
        via.Write(0x01, 0xa5);

        Assert.Equal(0xaf, via.Read(0x01));
    }
}
