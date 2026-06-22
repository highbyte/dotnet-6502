using Highbyte.DotNet6502.Systems.Commodore64.Cartridge;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Transport;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Cartridge;

public class SwiftLinkDeviceTests
{
    private const byte ReceiveIrqEnabledCommand = 0x09;
    private const byte TransmitIrqEnabledCommand = 0x05;

    [Fact]
    public async Task Rx_Full_Is_Set_When_Transport_Has_Data_And_Cleared_On_Read()
    {
        var device = new SwiftLinkDevice(C64CartridgeIOAddress.DE00, NullLogger<SwiftLinkDevice>.Instance);
        var transport = new LoopbackTransport();
        await transport.ConnectAsync();
        device.Transport = transport;

        var mem = MapDevice(device);

        await transport.SendAsync(0x41);
        device.Tick();

        Assert.True(IsRxFull(mem.Read(0xDE01)));
        Assert.Equal(0x41, mem.Read(0xDE00));
        Assert.False(IsRxFull(mem.Read(0xDE01)));
    }

    [Fact]
    public void Tx_Empty_Clears_On_Write_And_Sets_After_Send_Completes()
    {
        var transport = new PendingSendTransport();
        var device = new SwiftLinkDevice(C64CartridgeIOAddress.DE00, NullLogger<SwiftLinkDevice>.Instance)
        {
            Transport = transport
        };

        var mem = MapDevice(device);

        mem.Write(0xDE00, 0x42);
        Assert.False(IsTxEmpty(mem.Read(0xDE01)));

        transport.CompletePendingSend();
        device.Tick();

        Assert.True(IsTxEmpty(mem.Read(0xDE01)));
    }

    [Theory]
    [InlineData(C64CartridgeIOAddress.DE00, (ushort)0xDE00)]
    [InlineData(C64CartridgeIOAddress.DF00, (ushort)0xDF00)]
    public void Device_Maps_At_Configured_Base_Address(C64CartridgeIOAddress cartridgeIOAddress, ushort expectedBaseAddress)
    {
        var device = new SwiftLinkDevice(cartridgeIOAddress, NullLogger<SwiftLinkDevice>.Instance);
        var mem = MapDevice(device);

        mem.Write((ushort)(expectedBaseAddress + 0x02), 0x77);

        Assert.Equal(0x77, mem.Read((ushort)(expectedBaseAddress + 0x02)));
    }

    [Fact]
    public async Task Status_Write_Resets_Register_State()
    {
        var device = new SwiftLinkDevice(C64CartridgeIOAddress.DE00, NullLogger<SwiftLinkDevice>.Instance);
        var transport = new LoopbackTransport();
        await transport.ConnectAsync();
        device.Transport = transport;

        var mem = MapDevice(device);

        mem.Write(0xDE02, 0x12);
        mem.Write(0xDE03, 0x34);
        await transport.SendAsync(0x55);
        device.Tick();

        Assert.True(IsRxFull(mem.Read(0xDE01)));
        mem.Write(0xDE01, 0x00);

        Assert.Equal(0x00, mem.Read(0xDE02));
        Assert.Equal(0x00, mem.Read(0xDE03));
        Assert.False(IsRxFull(mem.Read(0xDE01)));
        Assert.True(IsTxEmpty(mem.Read(0xDE01)));
    }

    [Fact]
    public async Task Receive_Irq_Is_Raised_When_Rx_Full_Transitions_And_Cleared_On_Status_Read()
    {
        var interrupts = new CPUInterrupts();
        var device = new SwiftLinkDevice(C64CartridgeIOAddress.DE00, NullLogger<SwiftLinkDevice>.Instance)
        {
            CpuInterrupts = interrupts
        };
        var transport = new LoopbackTransport();
        await transport.ConnectAsync();
        device.Transport = transport;

        var mem = MapDevice(device);
        mem.Write(0xDE02, ReceiveIrqEnabledCommand);

        await transport.SendAsync(0x41);
        device.Tick();

        Assert.True(interrupts.IRQLineEnabled);
        var status = mem.Read(0xDE01);
        Assert.True(IsRxFull(status));
        Assert.True(IsIrqPending(status));
        Assert.False(interrupts.IRQLineEnabled);

        Assert.Equal(0x41, mem.Read(0xDE00));
        Assert.False(IsIrqPending(mem.Read(0xDE01)));
    }

    [Fact]
    public async Task Receive_Nmi_Is_Raised_When_Configured_And_Cleared_On_Status_Read()
    {
        var interrupts = new CPUInterrupts();
        var device = new SwiftLinkDevice(C64CartridgeIOAddress.DE00, NullLogger<SwiftLinkDevice>.Instance)
        {
            CpuInterrupts = interrupts,
            InterruptMode = C64SwiftLinkInterruptMode.NMI
        };
        var transport = new LoopbackTransport();
        await transport.ConnectAsync();
        device.Transport = transport;

        var mem = MapDevice(device);
        mem.Write(0xDE02, ReceiveIrqEnabledCommand);

        await transport.SendAsync(0x41);
        device.Tick();

        Assert.True(interrupts.NMILineEnabled);
        var status = mem.Read(0xDE01);
        Assert.True(IsRxFull(status));
        Assert.True(IsIrqPending(status));
        Assert.False(interrupts.NMILineEnabled);
    }

    [Fact]
    public void Transmit_Irq_Is_Raised_When_Send_Completes_And_Cleared_On_Status_Read()
    {
        var interrupts = new CPUInterrupts();
        var transport = new PendingSendTransport();
        var device = new SwiftLinkDevice(C64CartridgeIOAddress.DE00, NullLogger<SwiftLinkDevice>.Instance)
        {
            Transport = transport,
            CpuInterrupts = interrupts
        };

        var mem = MapDevice(device);
        mem.Write(0xDE02, TransmitIrqEnabledCommand);

        mem.Write(0xDE00, 0x42);
        Assert.False(interrupts.IRQLineEnabled);

        transport.CompletePendingSend();
        device.Tick();

        Assert.True(interrupts.IRQLineEnabled);
        var status = mem.Read(0xDE01);
        Assert.True(IsTxEmpty(status));
        Assert.True(IsIrqPending(status));
        Assert.False(interrupts.IRQLineEnabled);
    }

    [Fact]
    public async Task Rx_Queue_Behaves_As_Network_Side_Fifo_While_C64_Side_Stays_One_Byte()
    {
        var device = new SwiftLinkDevice(C64CartridgeIOAddress.DE00, NullLogger<SwiftLinkDevice>.Instance);
        var transport = new LoopbackTransport();
        await transport.ConnectAsync();
        device.Transport = transport;

        var mem = MapDevice(device);

        await transport.SendAsync(0x41);
        await transport.SendAsync(0x42);

        device.Tick();
        Assert.True(IsRxFull(mem.Read(0xDE01)));
        Assert.Equal(0x41, mem.Read(0xDE00));

        device.Tick();
        Assert.True(IsRxFull(mem.Read(0xDE01)));
        Assert.Equal(0x42, mem.Read(0xDE00));
    }

    [Fact]
    public async Task Compatible_Receive_Mode_Paces_Queued_Bytes_By_Cpu_Cycles()
    {
        ulong currentCycles = 0;
        var device = new SwiftLinkDevice(C64CartridgeIOAddress.DE00, NullLogger<SwiftLinkDevice>.Instance)
        {
            ReceiveMode = C64SwiftLinkReceiveMode.Compatible,
            ReceivePacingCycles = 10,
            GetCurrentCycleCount = () => currentCycles,
        };
        var transport = new LoopbackTransport();
        await transport.ConnectAsync();
        device.Transport = transport;

        var mem = MapDevice(device);

        await transport.SendAsync(0x41);
        await transport.SendAsync(0x42);

        device.Tick();
        Assert.True(IsRxFull(mem.Read(0xDE01)));
        Assert.Equal(0x41, mem.Read(0xDE00));

        Assert.False(IsRxFull(mem.Read(0xDE01)));

        currentCycles = 9;
        device.Tick();
        Assert.False(IsRxFull(mem.Read(0xDE01)));

        currentCycles = 10;
        device.Tick();
        Assert.True(IsRxFull(mem.Read(0xDE01)));
        Assert.Equal(0x42, mem.Read(0xDE00));
    }

    [Fact]
    public async Task FastBuffered_Receive_Mode_Latches_Queued_Byte_Without_New_Acia_Access()
    {
        var device = new SwiftLinkDevice(C64CartridgeIOAddress.DE00, NullLogger<SwiftLinkDevice>.Instance)
        {
            ReceiveMode = C64SwiftLinkReceiveMode.FastBuffered
        };
        var transport = new LoopbackTransport();
        await transport.ConnectAsync();
        device.Transport = transport;

        var mem = MapDevice(device);

        mem.Read(0xDE01);
        device.Tick();

        await transport.SendAsync(0x41);
        device.Tick();

        Assert.True(IsRxFull(mem.Read(0xDE01)));
        Assert.Equal(0x41, mem.Read(0xDE00));
    }

    [Fact]
    public async Task Receive_Irq_Is_Not_Raised_When_Disabled()
    {
        var interrupts = new CPUInterrupts();
        var device = new SwiftLinkDevice(C64CartridgeIOAddress.DE00, NullLogger<SwiftLinkDevice>.Instance)
        {
            CpuInterrupts = interrupts
        };
        var transport = new LoopbackTransport();
        await transport.ConnectAsync();
        device.Transport = transport;

        var mem = MapDevice(device);

        await transport.SendAsync(0x41);
        device.Tick();

        Assert.False(interrupts.IRQLineEnabled);
        Assert.True(IsRxFull(mem.Read(0xDE01)));
        Assert.False(IsIrqPending(mem.Read(0xDE01)));
    }

    [Fact]
    public async Task Status_Reflects_Carrier_And_Dsr_State_From_Transport_Connection()
    {
        var device = new SwiftLinkDevice(C64CartridgeIOAddress.DE00, NullLogger<SwiftLinkDevice>.Instance);
        var transport = new LoopbackTransport();
        device.Transport = transport;

        var mem = MapDevice(device);

        Assert.False(IsDcdHigh(mem.Read(0xDE01)));
        Assert.True(IsDsrHigh(mem.Read(0xDE01)));

        await transport.ConnectAsync();
        device.Tick();

        Assert.False(IsDcdHigh(mem.Read(0xDE01)));
        Assert.False(IsDsrHigh(mem.Read(0xDE01)));
    }

    [Fact]
    public void BuildC64_Creates_SwiftLink_Device_When_Enabled()
    {
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            SwiftLink =
            {
                Enabled = true,
                CartridgeIOAddress = C64CartridgeIOAddress.DF00,
            },
        }, new NullLoggerFactory());

        var swiftLink = c64.CartridgeSlot.GetCartridge<SwiftLinkDevice>();

        Assert.NotNull(swiftLink);
        Assert.Equal((ushort)0xDF00, swiftLink!.BaseAddress);
        Assert.Same(c64.CPU.CPUInterrupts, swiftLink.CpuInterrupts);
        Assert.Equal(C64SwiftLinkInterruptMode.IRQ, swiftLink.InterruptMode);
        Assert.Equal(C64SwiftLinkReceiveMode.Compatible, swiftLink.ReceiveMode);
        Assert.Equal(C64CartridgeLines.Released, swiftLink.Lines);
        Assert.Equal(31, c64.CurrentBank);

        c64.Mem.Write(0xDF02, 0x01);
        Assert.Equal(0x01, c64.Mem.Read(0xDF02));
    }

    [Fact]
    public void BuildC64_Does_Not_Create_SwiftLink_Device_When_Disabled()
    {
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            SwiftLink =
            {
                Enabled = false,
            },
        }, new NullLoggerFactory());

        Assert.Null(c64.CartridgeSlot.AttachedCartridge);
    }

    [Fact]
    public void Cleanup_Detaches_SwiftLink_And_Disposes_Its_Transport()
    {
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            SwiftLink =
            {
                Enabled = true,
            },
        }, new NullLoggerFactory());
        var swiftLink = c64.CartridgeSlot.GetCartridge<SwiftLinkDevice>()!;
        var transport = new PendingSendTransport();
        swiftLink.Transport = transport;

        c64.Cleanup();

        Assert.Null(c64.CartridgeSlot.AttachedCartridge);
        Assert.Equal(1, transport.DisposeCalls);
    }

    [Fact]
    public void SwiftLink_Can_Be_Attached_And_Detached_After_C64_Memory_Is_Created()
    {
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            SwiftLink =
            {
                Enabled = false,
            },
        }, new NullLoggerFactory());
        c64.Mem.Write(0xDE02, 0x5A);
        var swiftLink = new SwiftLinkDevice(
            C64CartridgeIOAddress.DE00,
            NullLogger<SwiftLinkDevice>.Instance)
        {
            CpuInterrupts = c64.CPU.CPUInterrupts,
        };

        c64.AttachCartridge(swiftLink);
        c64.Mem.Write(0xDE02, 0x77);

        Assert.Equal(0x77, c64.Mem.Read(0xDE02));

        c64.DetachCartridge();

        Assert.Null(c64.CartridgeSlot.AttachedCartridge);
        Assert.Equal(0x5A, c64.Mem.Read(0xDE02));
    }

    [Fact]
    public async Task Detaching_SwiftLink_Clears_Its_Interrupt_Line()
    {
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            SwiftLink =
            {
                Enabled = false,
            },
        }, new NullLoggerFactory());
        var swiftLink = new SwiftLinkDevice(
            C64CartridgeIOAddress.DE00,
            NullLogger<SwiftLinkDevice>.Instance)
        {
            CpuInterrupts = c64.CPU.CPUInterrupts,
        };
        c64.AttachCartridge(swiftLink);
        var transport = new LoopbackTransport();
        await transport.ConnectAsync();
        swiftLink.Transport = transport;
        c64.Mem.Write(0xDE02, ReceiveIrqEnabledCommand);
        await transport.SendAsync(0x41);
        c64.CartridgeSlot.Tick();
        Assert.True(c64.CPU.CPUInterrupts.IRQLineEnabled);

        c64.DetachCartridge();

        Assert.False(c64.CPU.CPUInterrupts.IRQLineEnabled);
        Assert.False(transport.IsConnected);
    }

    private static bool IsRxFull(byte status)
        => (status & (1 << 3)) != 0;

    private static Memory MapDevice(SwiftLinkDevice device)
    {
        var memory = new Memory();
        var io = new byte[0x200];
        var slot = new C64CartridgeSlot();
        slot.MapIOLocations(
            memory,
            address => io[address - 0xDE00],
            (address, value) => io[address - 0xDE00] = value);
        slot.Attach(device);
        return memory;
    }

    private static bool IsTxEmpty(byte status)
        => (status & (1 << 4)) != 0;

    private static bool IsIrqPending(byte status)
        => (status & (1 << 7)) != 0;

    private static bool IsDcdHigh(byte status)
        => (status & (1 << 5)) != 0;

    private static bool IsDsrHigh(byte status)
        => (status & (1 << 6)) != 0;

    private sealed class PendingSendTransport : ISwiftLinkTransport
    {
        private TaskCompletionSource _sendCompletionSource = NewCompletionSource();

        public bool IsConnected => true;
        public bool IsCarrierDetected => true;
        public bool IsDataSetReady => true;
        public int DisposeCalls { get; private set; }

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public bool TryDequeueReceivedByte(out byte value)
        {
            value = 0;
            return false;
        }

        public ValueTask SendAsync(byte value, CancellationToken cancellationToken = default)
            => new(_sendCompletionSource.Task);

        public void Reset()
        {
            _sendCompletionSource = NewCompletionSource();
        }

        public void Dispose()
        {
            DisposeCalls++;
        }

        public void CompletePendingSend()
        {
            _sendCompletionSource.TrySetResult();
        }

        private static TaskCompletionSource NewCompletionSource()
            => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
