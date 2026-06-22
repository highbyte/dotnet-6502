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
        var io = new byte[0x200];
        slot.MapIOLocations(
            memory,
            address => io[address - 0xDE00],
            (address, value) => io[address - 0xDE00] = value);
        slot.Attach(cartridge);

        memory.Write(0xDE00, 0x42);
        var value = memory.Read(0xDE00);
        slot.Tick();
        slot.Reset();

        Assert.Equal(0x42, value);
        Assert.Equal(1, cartridge.WriteIOCalls);
        Assert.Equal(1, cartridge.ReadIOCalls);
        Assert.Equal(1, cartridge.TickCalls);
        Assert.Equal(2, cartridge.ResetCalls);
    }

    [Fact]
    public void Detach_Disposes_And_Removes_The_Attached_Cartridge()
    {
        var slot = new C64CartridgeSlot();
        var cartridge = new TestCartridge();
        slot.Attach(cartridge);

        slot.Detach();

        Assert.Null(slot.AttachedCartridge);
        Assert.Equal(2, cartridge.ResetCalls);
        Assert.Equal(1, cartridge.DisposeCalls);
    }

    private sealed class TestCartridge(string name = "Test") : IC64Cartridge
    {
        public string Name { get; } = name;
        public C64CartridgeLines Lines => C64CartridgeLines.Released;
        public event Action? LinesChanged
        {
            add { }
            remove { }
        }
        private byte _ioValue;
        public int ReadIOCalls { get; private set; }
        public int WriteIOCalls { get; private set; }
        public int TickCalls { get; private set; }
        public int ResetCalls { get; private set; }
        public int DisposeCalls { get; private set; }

        public bool HandlesIOAddress(ushort address) => address == 0xDE00;
        public byte ReadIO(ushort address)
        {
            ReadIOCalls++;
            return _ioValue;
        }
        public void WriteIO(ushort address, byte value)
        {
            WriteIOCalls++;
            _ioValue = value;
        }
        public bool HasROML => false;
        public byte ReadROML(ushort address) => throw new InvalidOperationException();
        public bool HasROMH => false;
        public byte ReadROMH(ushort address) => throw new InvalidOperationException();
        public void Tick() => TickCalls++;
        public void Reset() => ResetCalls++;
        public void Dispose() => DisposeCalls++;
    }
}
