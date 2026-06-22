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

    [Fact]
    public void Replace_Activates_New_Cartridge_And_Disposes_Previous_Cartridge()
    {
        var slot = new C64CartridgeSlot();
        var previous = new TestCartridge("Previous");
        var replacement = new TestCartridge("Replacement");
        var lineChanges = 0;
        slot.LinesChanged += () => lineChanges++;
        slot.Attach(previous);

        slot.Replace(replacement);
        previous.RaiseLinesChanged();
        replacement.RaiseLinesChanged();

        Assert.Same(replacement, slot.AttachedCartridge);
        Assert.Equal(2, previous.ResetCalls);
        Assert.Equal(1, previous.DisposeCalls);
        Assert.Equal(1, replacement.ResetCalls);
        Assert.Equal(3, lineChanges);
    }

    [Fact]
    public void Replace_Preserves_Previous_Cartridge_When_Replacement_Reset_Fails()
    {
        var slot = new C64CartridgeSlot();
        var previous = new TestCartridge("Previous");
        var replacement = new TestCartridge("Replacement", throwOnReset: true);
        slot.Attach(previous);

        Assert.Throws<InvalidOperationException>(() => slot.Replace(replacement));

        Assert.Same(previous, slot.AttachedCartridge);
        Assert.Equal(1, previous.ResetCalls);
        Assert.Equal(0, previous.DisposeCalls);
        Assert.Equal(1, replacement.ResetCalls);
    }

    private sealed class TestCartridge(
        string name = "Test",
        bool throwOnReset = false) : IC64Cartridge
    {
        public string Name { get; } = name;
        public C64CartridgeLines Lines => C64CartridgeLines.Released;
        public event Action? LinesChanged;
        private byte _ioValue;
        public int ReadIOCalls { get; private set; }
        public int WriteIOCalls { get; private set; }
        public int TickCalls { get; private set; }
        public int ResetCalls { get; private set; }
        public int DisposeCalls { get; private set; }

        public bool HandlesIORead(ushort address) => address == 0xDE00;
        public bool HandlesIOWrite(ushort address) => address == 0xDE00;
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
        public void Reset()
        {
            ResetCalls++;
            if (throwOnReset)
                throw new InvalidOperationException("Reset failed.");
        }
        public void Dispose() => DisposeCalls++;
        public void RaiseLinesChanged() => LinesChanged?.Invoke();
    }
}
