using Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Cartridge;

public class C64CartridgeSlotTests
{
    [Fact]
    public void Attach_Exposes_The_Attached_Cartridge()
    {
        var slot = new C64CartridgeSlot();
        var cartridge = new TestCartridge();

        slot.Attach(cartridge);

        Assert.Same(cartridge, slot.AttachedCartridge);
        Assert.Same(cartridge, slot.GetCartridge<TestCartridge>());
    }

    [Fact]
    public void Attach_Rejects_A_Second_Cartridge()
    {
        var slot = new C64CartridgeSlot();
        slot.Attach(new TestCartridge("First"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => slot.Attach(new TestCartridge("Second")));

        Assert.Equal("Cartridge 'First' is already attached.", exception.Message);
    }

    [Fact]
    public void Slot_Routes_Mapping_Tick_And_Reset_To_The_Attached_Cartridge()
    {
        var slot = new C64CartridgeSlot();
        var cartridge = new TestCartridge();
        var memory = new Memory();
        slot.Attach(cartridge);

        slot.MapIOLocations(memory);
        slot.MapROMLLocations(memory);
        slot.MapROMHLocations(memory);
        slot.Tick();
        slot.Reset();

        Assert.Equal(1, cartridge.MapIOCalls);
        Assert.Equal(1, cartridge.MapROMLCalls);
        Assert.Equal(1, cartridge.MapROMHCalls);
        Assert.Equal(1, cartridge.TickCalls);
        Assert.Equal(1, cartridge.ResetCalls);
    }

    [Fact]
    public void Detach_Disposes_And_Removes_The_Attached_Cartridge()
    {
        var slot = new C64CartridgeSlot();
        var cartridge = new TestCartridge();
        slot.Attach(cartridge);

        slot.Detach();

        Assert.Null(slot.AttachedCartridge);
        Assert.Equal(1, cartridge.DisposeCalls);
    }

    private sealed class TestCartridge(string name = "Test") : IC64Cartridge
    {
        public string Name { get; } = name;
        public int MapIOCalls { get; private set; }
        public int MapROMLCalls { get; private set; }
        public int MapROMHCalls { get; private set; }
        public int TickCalls { get; private set; }
        public int ResetCalls { get; private set; }
        public int DisposeCalls { get; private set; }

        public void MapIOLocations(Memory mem) => MapIOCalls++;
        public void MapROMLLocations(Memory mem) => MapROMLCalls++;
        public void MapROMHLocations(Memory mem) => MapROMHCalls++;
        public void Tick() => TickCalls++;
        public void Reset() => ResetCalls++;
        public void Dispose() => DisposeCalls++;
    }
}
