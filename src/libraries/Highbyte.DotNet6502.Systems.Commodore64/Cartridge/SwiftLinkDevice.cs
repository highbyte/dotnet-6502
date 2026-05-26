using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Transport;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

public sealed class SwiftLinkDevice : IC64CartridgeDevice
{
    private const byte StatusRxFullBit = 1 << 3;
    private const byte StatusTxEmptyBit = 1 << 4;
    private const byte StatusIrqBit = 1 << 7;

    private readonly ILogger _logger;
    private readonly ushort _baseAddress;

    private byte _rxData;
    private bool _rxFull;
    private bool _txEmpty = true;
    private bool _irqPending;
    private byte _commandRegister;
    private byte _controlRegister;
    private Task? _pendingSendTask;

    public SwiftLinkDevice(C64CartridgeIOAddress baseAddress, ILogger logger)
    {
        _baseAddress = baseAddress == C64CartridgeIOAddress.DF00 ? (ushort)0xDF00 : (ushort)0xDE00;
        _logger = logger;
    }

    public string Name => "SwiftLink";
    public ISwiftLinkTransport? Transport { get; set; }
    public ushort BaseAddress => _baseAddress;

    public void MapIOLocations(Memory mem)
    {
        mem.MapReader((ushort)(_baseAddress + 0x00), DataLoad);
        mem.MapWriter((ushort)(_baseAddress + 0x00), DataStore);
        mem.MapReader((ushort)(_baseAddress + 0x01), StatusLoad);
        mem.MapWriter((ushort)(_baseAddress + 0x01), StatusStore);
        mem.MapReader((ushort)(_baseAddress + 0x02), (_) => _commandRegister);
        mem.MapWriter((ushort)(_baseAddress + 0x02), CommandStore);
        mem.MapReader((ushort)(_baseAddress + 0x03), (_) => _controlRegister);
        mem.MapWriter((ushort)(_baseAddress + 0x03), ControlStore);
    }

    public void Tick()
    {
        if (_pendingSendTask != null && _pendingSendTask.IsCompleted)
        {
            CompletePendingSend();
        }

        if (!_rxFull && Transport?.TryDequeueReceivedByte(out var value) == true)
        {
            _rxData = value;
            _rxFull = true;
        }
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
        Transport?.Reset();
    }

    private byte DataLoad(ushort _)
    {
        var value = _rxData;
        _rxFull = false;
        return value;
    }

    private void DataStore(ushort _, byte value)
    {
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
        byte value = 0;
        if (_rxFull)
            value |= StatusRxFullBit;
        if (_txEmpty)
            value |= StatusTxEmptyBit;
        if (_irqPending)
            value |= StatusIrqBit;
        return value;
    }

    private void StatusStore(ushort _, byte __)
    {
        Reset();
    }

    private void CommandStore(ushort _, byte value)
    {
        _commandRegister = value;
    }

    private void ControlStore(ushort _, byte value)
    {
        _controlRegister = value;
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
    }
}
