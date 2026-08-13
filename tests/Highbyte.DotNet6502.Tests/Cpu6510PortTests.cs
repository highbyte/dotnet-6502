using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Tests;

public class Cpu6510PortTests
{
    [Fact]
    public void ReadPort_Combines_Outputs_Inputs_And_Unimplemented_Bits_Per_Bit()
    {
        var port = new Cpu6510Port();
        // P0-P2 + P4 pulled high by the board (C64-style wiring), P3/P5 low.
        port.ExternalInputLevels = 0b0001_0111;

        // All implemented lines outputs: reads follow the latch, inputs invisible.
        port.SetState(dataDirectionRegister: 0b0011_1111, dataRegister: 0b0010_1010);
        Assert.Equal(0b0010_1010, port.ReadPort());

        // All implemented lines inputs: reads follow the external levels.
        port.SetState(dataDirectionRegister: 0b0000_0000, dataRegister: 0b0011_1111);
        Assert.Equal(0b0001_0111, port.ReadPort());

        // Mixed: P0-P2 outputs (latch 0b101), P3-P5 inputs (external levels contribute
        // only P4). Unimplemented bits 6-7 always read the latch.
        port.SetState(dataDirectionRegister: 0b0000_0111, dataRegister: 0b1100_0101);
        Assert.Equal(0b1101_0101, port.ReadPort());
    }

    [Fact]
    public void ReadDataDirectionRegister_Returns_Raw_Value()
    {
        var port = new Cpu6510Port();
        port.WriteDataDirectionRegister(0x2F);
        Assert.Equal(0x2F, port.ReadDataDirectionRegister());
    }

    [Fact]
    public void Register_Writes_Notify_Synchronously_Even_When_Value_Is_Unchanged()
    {
        var port = new Cpu6510Port();
        var notifications = 0;
        port.OutputsChanged += () => notifications++;

        port.WriteDataDirectionRegister(0x2F);
        port.WriteDataDirectionRegister(0x2F); // unchanged value still notifies
        port.WriteDataRegister(0x37);

        Assert.Equal(3, notifications);
    }

    [Fact]
    public void Notification_Observes_The_Already_Updated_State()
    {
        var port = new Cpu6510Port();
        byte seenLatch = 0;
        port.OutputsChanged += () => seenLatch = port.DataRegister;

        port.WriteDataRegister(0x35);

        Assert.Equal(0x35, seenLatch);
    }

    [Fact]
    public void SetState_Sets_Both_Registers_Then_Notifies_Exactly_Once()
    {
        var port = new Cpu6510Port();
        var notifications = 0;
        (byte Ddr, byte Data) seen = default;
        port.OutputsChanged += () =>
        {
            notifications++;
            seen = (port.DataDirectionRegister, port.DataRegister);
        };

        port.SetState(dataDirectionRegister: 0x2F, dataRegister: 0x37);

        Assert.Equal(1, notifications);
        Assert.Equal((0x2F, 0x37), seen); // never a half-applied combination
    }

    [Fact]
    public void Reads_Do_Not_Notify()
    {
        var port = new Cpu6510Port();
        port.SetState(0x2F, 0x37);
        var notifications = 0;
        port.OutputsChanged += () => notifications++;

        port.ReadPort();
        port.ReadDataDirectionRegister();

        Assert.Equal(0, notifications);
    }

    [Fact]
    public void SerializeState_Returns_The_Two_Raw_Registers()
    {
        var port = new Cpu6510Port();
        port.SetState(dataDirectionRegister: 0x2F, dataRegister: 0x35);

        Assert.Equal(new byte[] { 0x2F, 0x35 }, port.SerializeState());
    }

    [Fact]
    public void RestoreState_Applies_Atomically_With_One_Notification()
    {
        var port = new Cpu6510Port();
        var notifications = 0;
        (byte Ddr, byte Data) seen = default;
        port.OutputsChanged += () =>
        {
            notifications++;
            seen = (port.DataDirectionRegister, port.DataRegister);
        };

        port.RestoreState(new byte[] { 0x2F, 0x35 });

        Assert.Equal(1, notifications);
        Assert.Equal((0x2F, 0x35), seen);
    }

    [Fact]
    public void Serialize_Then_Restore_Round_Trips_On_A_Fresh_Port()
    {
        var source = new Cpu6510Port();
        source.SetState(0x2F, 0x37);

        var target = new Cpu6510Port();
        target.RestoreState(source.SerializeState());

        Assert.Equal(0x2F, target.DataDirectionRegister);
        Assert.Equal(0x37, target.DataRegister);
    }

    [Fact]
    public void RestoreState_Rejects_A_Payload_That_Is_Not_Two_Bytes()
    {
        var port = new Cpu6510Port();

        Assert.Throws<DotNet6502Exception>(() => port.RestoreState(new byte[] { 0x2F }));
        Assert.Throws<DotNet6502Exception>(() => port.RestoreState(new byte[] { 0x2F, 0x35, 0x00 }));
    }

    [Fact]
    public void Clone_Copies_State_But_Not_Subscribers()
    {
        var port = new Cpu6510Port();
        port.ExternalInputLevels = 0x17;
        port.SetState(0x2F, 0x37);
        var originalNotifications = 0;
        port.OutputsChanged += () => originalNotifications++;
        originalNotifications = 0; // ignore the SetState call above (subscribed after anyway)

        var clone = (Cpu6510Port)port.Clone();

        Assert.Equal(port.DataDirectionRegister, clone.DataDirectionRegister);
        Assert.Equal(port.DataRegister, clone.DataRegister);
        Assert.Equal(port.ExternalInputLevels, clone.ExternalInputLevels);

        // Writing the clone must not call back into the original machine's subscriber.
        clone.WriteDataRegister(0x30);
        Assert.Equal(0, originalNotifications);
        // And the clone is independent state.
        Assert.NotEqual(port.DataRegister, clone.DataRegister);
    }
}

public class Mos6510ModelTests
{
    [Fact]
    public void Cpu_Constructed_With_Mos6510_Model_Has_A_Port_Instance()
    {
        var cpu = new CPU(new ExecState(), new NullLoggerFactory(), CpuModelIds.Mos6510, CpuCompatibilityProfile.ExperimentalUnofficial);

        Assert.Equal(CpuModelIds.Mos6510, cpu.CpuModelId);
        Assert.IsType<Cpu6510Port>(cpu.ModelState);
    }

    [Fact]
    public void Each_Mos6510_Cpu_Gets_Its_Own_Port_Instance()
    {
        var cpu1 = new CPU(new ExecState(), new NullLoggerFactory(), CpuModelIds.Mos6510, CpuCompatibilityProfile.ExperimentalUnofficial);
        var cpu2 = new CPU(new ExecState(), new NullLoggerFactory(), CpuModelIds.Mos6510, CpuCompatibilityProfile.ExperimentalUnofficial);

        Assert.NotSame(cpu1.ModelState, cpu2.ModelState);
    }

    [Fact]
    public void Other_Models_Have_No_Model_State()
    {
        var nmos = new CPU(new ExecState(), new NullLoggerFactory(), CpuModelIds.Nmos6502, CpuCompatibilityProfile.ExperimentalUnofficial);
        var cmos = new CPU(new ExecState(), new NullLoggerFactory(), CpuModelIds.Ncr65c02, CpuCompatibilityProfile.OfficialOnly);

        Assert.Null(nmos.ModelState);
        Assert.Null(cmos.ModelState);
    }

    [Theory]
    [InlineData(CpuCompatibilityProfile.OfficialOnly)]
    [InlineData(CpuCompatibilityProfile.StableUnofficial)]
    [InlineData(CpuCompatibilityProfile.ExperimentalUnofficial)]
    [InlineData(CpuCompatibilityProfile.FullUnofficial)]
    public void Mos6510_Descriptor_Table_Is_Identical_To_Nmos6502(CpuCompatibilityProfile profile)
    {
        // The 6510 is an NMOS 6502 core with an added I/O port: instruction behavior
        // must be byte-for-byte the same table (shared delegates, not just equal data).
        var mos6510 = new CPU(new ExecState(), new NullLoggerFactory(), CpuModelIds.Mos6510, profile);
        var nmos6502 = new CPU(new ExecState(), new NullLoggerFactory(), CpuModelIds.Nmos6502, profile);

        for (var code = 0; code <= 0xff; code++)
        {
            var a = mos6510.Descriptors[code];
            var b = nmos6502.Descriptors[code];
            Assert.Equal(b is null, a is null);
            if (a is null || b is null)
                continue;
            Assert.Equal(b.Mnemonic, a.Mnemonic);
            Assert.Equal(b.Addressing, a.Addressing);
            Assert.Equal(b.Size, a.Size);
            Assert.Equal(b.BaseCycles, a.BaseCycles);
            Assert.Equal(b.Documented, a.Documented);
        }
    }

    [Fact]
    public void Cloned_Mos6510_Cpu_Gets_A_Cloned_Port_Without_Subscribers()
    {
        var cpu = new CPU(new ExecState(), new NullLoggerFactory(), CpuModelIds.Mos6510, CpuCompatibilityProfile.ExperimentalUnofficial);
        var port = (Cpu6510Port)cpu.ModelState!;
        port.ExternalInputLevels = 0x17;
        port.SetState(0x2F, 0x37);
        var originalNotifications = 0;
        port.OutputsChanged += () => originalNotifications++;

        var clone = cpu.Clone();
        var clonedPort = Assert.IsType<Cpu6510Port>(clone.ModelState);

        Assert.NotSame(port, clonedPort);
        Assert.Equal(port.DataDirectionRegister, clonedPort.DataDirectionRegister);
        Assert.Equal(port.DataRegister, clonedPort.DataRegister);
        Assert.Equal(port.ExternalInputLevels, clonedPort.ExternalInputLevels);

        clonedPort.WriteDataRegister(0x30);
        Assert.Equal(0, originalNotifications);
    }

    [Fact]
    public void Mos6510_Executes_Instructions_Like_An_Nmos6502()
    {
        // Smoke check through the full executor: LDA #$42 / STA $02 / INC $02.
        var cpu = new CPU(new ExecState(), new NullLoggerFactory(), CpuModelIds.Mos6510, CpuCompatibilityProfile.ExperimentalUnofficial);
        var mem = new Memory();
        byte[] program = { 0xA9, 0x42, 0x85, 0x02, 0xE6, 0x02 };
        for (var i = 0; i < program.Length; i++)
            mem[(ushort)(0x1000 + i)] = program[i];
        cpu.PC = 0x1000;

        cpu.ExecuteOneInstruction(mem);
        cpu.ExecuteOneInstruction(mem);
        cpu.ExecuteOneInstruction(mem);

        Assert.Equal(0x43, mem[0x02]);
    }
}
