using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Transport;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

public sealed class SwiftLinkDevice : IC64CartridgeDevice
{
    private const byte StatusRxFullBit = 1 << 3;
    private const byte StatusTxEmptyBit = 1 << 4;
    private const byte StatusDcdBit = 1 << 5;
    private const byte StatusDsrBit = 1 << 6;
    private const byte StatusIrqBit = 1 << 7;
    private const byte CommandDtrBit = 1 << 0;
    private const byte CommandReceiverIrqDisableBit = 1 << 1;
    private const byte CommandTransmitterControlMask = 0b0000_1100;
    private const byte CommandTransmitterIrqControl = 0b0000_0100;
    private const string IrqSourceName = "SwiftLink";

    private readonly ILogger _logger;
    private readonly ushort _baseAddress;

    private byte _rxData;
    private bool _rxFull;
    private bool _txEmpty = true;
    private bool _irqPending;
    private byte _commandRegister;
    private byte _controlRegister;
    private Task? _pendingSendTask;
    private bool? _lastConnectedState;
    private byte? _lastLoggedStatusValue;
    private int _statusReadCount;
    private bool _receivePollRequested = true;

    public SwiftLinkDevice(C64CartridgeIOAddress baseAddress, ILogger logger)
    {
        _baseAddress = baseAddress == C64CartridgeIOAddress.DF00 ? (ushort)0xDF00 : (ushort)0xDE00;
        _logger = logger;
    }

    public string Name => "SwiftLink";
    public ISwiftLinkTransport? Transport { get; set; }
    public CPUInterrupts? CpuInterrupts { get; set; }
    public C64SwiftLinkInterruptMode InterruptMode { get; set; } = C64SwiftLinkInterruptMode.IRQ;
    public C64SwiftLinkReceiveMode ReceiveMode { get; set; } = C64SwiftLinkReceiveMode.Compatible;
    public ushort BaseAddress => _baseAddress;

    public void MapIOLocations(Memory mem)
    {
        mem.MapReader((ushort)(_baseAddress + 0x00), DataLoad);
        mem.MapWriter((ushort)(_baseAddress + 0x00), DataStore);
        mem.MapReader((ushort)(_baseAddress + 0x01), StatusLoad);
        mem.MapWriter((ushort)(_baseAddress + 0x01), StatusStore);
        mem.MapReader((ushort)(_baseAddress + 0x02), CommandLoad);
        mem.MapWriter((ushort)(_baseAddress + 0x02), CommandStore);
        mem.MapReader((ushort)(_baseAddress + 0x03), ControlLoad);
        mem.MapWriter((ushort)(_baseAddress + 0x03), ControlStore);
    }

    public void Tick()
    {
        UpdateConnectionState();

        if (_pendingSendTask != null && _pendingSendTask.IsCompleted)
        {
            CompletePendingSend();
        }

        if (_rxFull)
            return;

        if (ReceiveMode == C64SwiftLinkReceiveMode.FastBuffered)
        {
            TryLatchReceivedByte();
            return;
        }

        if (!_receivePollRequested)
            return;

        _receivePollRequested = false;
        TryLatchReceivedByte();
    }

    public void Reset()
    {
        _rxData = 0;
        _rxFull = false;
        _txEmpty = true;
        _irqPending = false;
        _commandRegister = 0;
        _controlRegister = 0;
        _pendingSendTask = null;
        _lastConnectedState = null;
        _lastLoggedStatusValue = null;
        _statusReadCount = 0;
        _receivePollRequested = true;
        ClearIrqPending();
        Transport?.Reset();
    }

    private byte DataLoad(ushort _)
    {
        RequestReceivePoll();
        var value = _rxData;
        _rxFull = false;
        _logger.LogDebug("SwiftLink DATA read returned 0x{Value:X2}.", value);
        return value;
    }

    private void DataStore(ushort _, byte value)
    {
        RequestReceivePoll();
        if (!_txEmpty)
            return;

        _txEmpty = false;
        var transport = Transport;
        if (transport == null)
        {
            _txEmpty = true;
            return;
        }

        try
        {
            var sendTask = transport.SendAsync(value).AsTask();
            if (sendTask.IsCompleted)
            {
                _pendingSendTask = sendTask;
                CompletePendingSend();
            }
            else
            {
                _pendingSendTask = sendTask;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SwiftLink send failed.");
            _pendingSendTask = null;
            _txEmpty = true;
        }
    }

    private byte StatusLoad(ushort _)
    {
        RequestReceivePoll();
        byte value = 0;
        // Match the C64-specific behavior used by VICE's ACIA emulation more closely:
        // software tends to observe carrier readiness through the DSR bit, while DCD is
        // not surfaced in a useful way on C64-class machines.
        if (!IsDataSetReady)
            value |= StatusDsrBit;
        if (_rxFull)
            value |= StatusRxFullBit;
        if (_txEmpty)
            value |= StatusTxEmptyBit;
        if (_irqPending)
            value |= StatusIrqBit;

        _statusReadCount++;
        if (_statusReadCount <= 200)
        {
            _logger.LogDebug(
                "SwiftLink STATUS read #{Count}: 0x{Value:X2} (RX_FULL={RxFull}, TX_EMPTY={TxEmpty}, IRQ={IrqPending}, DSR_READY={DsrReady}, CARRIER={Carrier}).",
                _statusReadCount,
                value,
                _rxFull,
                _txEmpty,
                _irqPending,
                IsDataSetReady,
                IsCarrierDetected);
        }
        else if (_lastLoggedStatusValue != value)
        {
            _logger.LogDebug("SwiftLink STATUS read returned 0x{Value:X2}.", value);
            _lastLoggedStatusValue = value;
        }
        else
        {
            _lastLoggedStatusValue = value;
        }

        // The 6551 IRQ status flag is cleared by reading the status register.
        if (_irqPending)
            ClearIrqPending();

        return value;
    }

    private void StatusStore(ushort _, byte __)
    {
        RequestReceivePoll();
        Reset();
    }

    private byte CommandLoad(ushort _)
    {
        RequestReceivePoll();
        return _commandRegister;
    }

    private void CommandStore(ushort _, byte value)
    {
        RequestReceivePoll();
        _commandRegister = value;
        _logger.LogDebug("SwiftLink command register set to 0x{Value:X2}.", value);

        if (!InterruptsEnabled || !ReceiveInterruptEnabled)
            ClearIrqPending();
    }

    private byte ControlLoad(ushort _)
    {
        RequestReceivePoll();
        return _controlRegister;
    }

    private void ControlStore(ushort _, byte value)
    {
        RequestReceivePoll();
        _controlRegister = value;
        _logger.LogDebug("SwiftLink control register set to 0x{Value:X2}.", value);
    }

    private void CompletePendingSend()
    {
        var pendingSendTask = _pendingSendTask;
        _pendingSendTask = null;

        if (pendingSendTask == null)
        {
            _txEmpty = true;
            return;
        }

        if (pendingSendTask.IsFaulted)
            _logger.LogDebug(pendingSendTask.Exception, "SwiftLink send task faulted.");

        _txEmpty = true;
        RaiseTransmitIrqIfEnabled();
    }

    private bool InterruptsEnabled => (_commandRegister & CommandDtrBit) != 0;

    private bool ReceiveInterruptEnabled
        => InterruptsEnabled && (_commandRegister & CommandReceiverIrqDisableBit) == 0;

    private bool TransmitInterruptEnabled
        => InterruptsEnabled && (_commandRegister & CommandTransmitterControlMask) == CommandTransmitterIrqControl;

    private void RaiseReceiveIrqIfEnabled()
    {
        if (_rxFull && ReceiveInterruptEnabled)
            SetIrqPending();
    }

    private void RaiseTransmitIrqIfEnabled()
    {
        if (_txEmpty && TransmitInterruptEnabled)
            SetIrqPending();
    }

    private void SetIrqPending()
    {
        _irqPending = true;
        _logger.LogDebug(
            "SwiftLink {InterruptMode} pending set (RX_FULL={RxFull}, TX_EMPTY={TxEmpty}).",
            InterruptMode,
            _rxFull,
            _txEmpty);
        if (InterruptMode == C64SwiftLinkInterruptMode.NMI)
            CpuInterrupts?.SetNMISourceActive(IrqSourceName);
        else
            CpuInterrupts?.SetIRQSourceActive(IrqSourceName, autoAcknowledge: false);
    }

    private void ClearIrqPending()
    {
        _irqPending = false;
        _logger.LogDebug("SwiftLink {InterruptMode} pending cleared.", InterruptMode);
        if (InterruptMode == C64SwiftLinkInterruptMode.NMI)
            CpuInterrupts?.SetNMISourceInactive(IrqSourceName);
        else
            CpuInterrupts?.SetIRQSourceInactive(IrqSourceName);
    }

    private bool IsCarrierDetected => Transport?.IsCarrierDetected == true;

    private bool IsDataSetReady => Transport?.IsDataSetReady == true;

    private void RequestReceivePoll()
    {
        if (ReceiveMode == C64SwiftLinkReceiveMode.Compatible)
            _receivePollRequested = true;
    }

    private void TryLatchReceivedByte()
    {
        if (Transport?.TryDequeueReceivedByte(out var value) != true)
            return;

        _rxData = value;
        _rxFull = true;
        _logger.LogDebug("SwiftLink RX latched byte 0x{Value:X2}.", value);
        RaiseReceiveIrqIfEnabled();
    }

    private void UpdateConnectionState()
    {
        var isConnected = Transport?.IsConnected == true;
        if (_lastConnectedState == isConnected)
            return;

        var hadPreviousState = _lastConnectedState.HasValue;
        _lastConnectedState = isConnected;
        if (hadPreviousState)
            _logger.LogInformation("SwiftLink TCP transport {State}.", isConnected ? "connected" : "disconnected");
        else
            _logger.LogDebug("SwiftLink TCP transport initial state is {State}.", isConnected ? "connected" : "disconnected");

        if (hadPreviousState && ReceiveInterruptEnabled)
            SetIrqPending();
    }
}
