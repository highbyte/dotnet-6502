namespace Highbyte.DotNet6502.Systems.Oric.Hardware;

/// <summary>
/// MOS 6522 subset used by the Oric Atmos: parallel ports, control pins, timers and IRQs.
/// The callback-oriented ports keep the chip independent of Oric keyboard and AY wiring.
/// </summary>
public sealed class Via6522
{
    public const byte InterruptCa2 = 0x01;
    public const byte InterruptCa1 = 0x02;
    public const byte InterruptShiftRegister = 0x04;
    public const byte InterruptCb2 = 0x08;
    public const byte InterruptCb1 = 0x10;
    public const byte InterruptTimer2 = 0x20;
    public const byte InterruptTimer1 = 0x40;

    private readonly Func<byte> _readPortAInput;
    private readonly Func<byte> _readPortBInput;
    private readonly Action<byte> _writePortAOutput;
    private readonly Action<byte> _writePortBOutput;
    private readonly Action<bool> _writeCa2;
    private readonly Action<bool> _writeCb2;
    private readonly Action<bool> _irqChanged;

    private byte _portA;
    private byte _portB;
    private byte _ddrA;
    private byte _ddrB;
    private byte _shiftRegister;
    private byte _acr;
    private byte _pcr;
    private byte _ifr;
    private byte _ier;
    private ushort _timer1Counter;
    private ushort _timer1Latch;
    private ushort _timer2Counter;
    private byte _timer2LatchLow;
    private bool _timer1Running;
    private bool _timer2Running;
    private bool _ca1;
    private bool _cb1;
    private bool _ca2;
    private bool _cb2;

    public byte PortAOutput => _portA;
    public byte PortBOutput => _portB;
    public byte DataDirectionA => _ddrA;
    public byte DataDirectionB => _ddrB;
    public byte InterruptFlags => (byte)(_ifr | (IrqActive ? 0x80 : 0));
    public byte InterruptEnable => _ier;
    public bool IrqActive => (_ifr & _ier & 0x7f) != 0;
    public bool Ca2 => _ca2;
    public bool Cb2 => _cb2;

    public Via6522(
        Func<byte>? readPortAInput = null,
        Action<byte>? writePortAOutput = null,
        Func<byte>? readPortBInput = null,
        Action<byte>? writePortBOutput = null,
        Action<bool>? writeCa2 = null,
        Action<bool>? writeCb2 = null,
        Action<bool>? irqChanged = null)
    {
        _readPortAInput = readPortAInput ?? (() => 0xff);
        _writePortAOutput = writePortAOutput ?? (_ => { });
        _readPortBInput = readPortBInput ?? (() => 0xff);
        _writePortBOutput = writePortBOutput ?? (_ => { });
        _writeCa2 = writeCa2 ?? (_ => { });
        _writeCb2 = writeCb2 ?? (_ => { });
        _irqChanged = irqChanged ?? (_ => { });
        Reset();
    }

    public void Map(Memory memory, ushort startAddress = 0x0300, ushort endAddress = 0x03ff)
    {
        for (var address = (int)startAddress; address <= endAddress; address++)
        {
            var mappedAddress = (ushort)address;
            memory.MapReader(mappedAddress, Read);
            memory.MapWriter(mappedAddress, Write);
        }
    }

    public void Reset()
    {
        _portA = _portB = _ddrA = _ddrB = _shiftRegister = _acr = _pcr = _ifr = _ier = 0;
        _timer1Counter = _timer1Latch = _timer2Counter = 0xffff;
        _timer2LatchLow = 0xff;
        _timer1Running = _timer2Running = false;
        _ca1 = _cb1 = _ca2 = _cb2 = false;
        _writePortAOutput(_portA);
        _writePortBOutput(_portB);
        _writeCa2(_ca2);
        _writeCb2(_cb2);
        _irqChanged(false);
    }

    public byte Read(ushort address)
    {
        var register = address & 0x0f;
        return register switch
        {
            0x0 => ReadPortB(),
            0x1 => ReadPortA(clearHandshakeFlags: true),
            0x2 => _ddrB,
            0x3 => _ddrA,
            0x4 => ReadTimer1Low(),
            0x5 => (byte)(_timer1Counter >> 8),
            0x6 => (byte)_timer1Latch,
            0x7 => (byte)(_timer1Latch >> 8),
            0x8 => ReadTimer2Low(),
            0x9 => (byte)(_timer2Counter >> 8),
            0xa => _shiftRegister,
            0xb => _acr,
            0xc => _pcr,
            0xd => InterruptFlags,
            0xe => (byte)(_ier | 0x80),
            0xf => ReadPortA(clearHandshakeFlags: false),
            _ => 0xff,
        };
    }

    public void Write(ushort address, byte value)
    {
        switch (address & 0x0f)
        {
            case 0x0: WritePortB(value); break;
            case 0x1: WritePortA(value, clearHandshakeFlags: true); break;
            case 0x2: _ddrB = value; _writePortBOutput(_portB); break;
            case 0x3: _ddrA = value; _writePortAOutput(_portA); break;
            case 0x4: _timer1Latch = (ushort)((_timer1Latch & 0xff00) | value); break;
            case 0x5:
                _timer1Latch = (ushort)((value << 8) | (_timer1Latch & 0x00ff));
                _timer1Counter = _timer1Latch;
                _timer1Running = true;
                ClearInterrupt(InterruptTimer1);
                break;
            case 0x6: _timer1Latch = (ushort)((_timer1Latch & 0xff00) | value); break;
            case 0x7: _timer1Latch = (ushort)((value << 8) | (_timer1Latch & 0x00ff)); break;
            case 0x8: _timer2LatchLow = value; break;
            case 0x9:
                _timer2Counter = (ushort)((value << 8) | _timer2LatchLow);
                _timer2Running = true;
                ClearInterrupt(InterruptTimer2);
                break;
            case 0xa: _shiftRegister = value; ClearInterrupt(InterruptShiftRegister); break;
            case 0xb: _acr = value; break;
            case 0xc: WritePeripheralControl(value); break;
            case 0xd: ClearInterrupt((byte)(value & 0x7f)); break;
            case 0xe:
                if ((value & 0x80) != 0)
                    _ier |= (byte)(value & 0x7f);
                else
                    _ier &= (byte)~value;
                UpdateIrq();
                break;
            case 0xf: WritePortA(value, clearHandshakeFlags: false); break;
        }
    }

    public void ProcessCycles(int cycles)
    {
        if (cycles <= 0)
            return;
        TickTimer1(cycles);
        TickTimer2(cycles);
    }

    public void SetCa1(bool level)
    {
        var activeEdge = ((_pcr & 0x01) != 0) ? !_ca1 && level : _ca1 && !level;
        _ca1 = level;
        if (activeEdge)
            SetInterrupt(InterruptCa1);
    }

    public void SetCb1(bool level)
    {
        var activeEdge = ((_pcr & 0x10) != 0) ? !_cb1 && level : _cb1 && !level;
        _cb1 = level;
        if (activeEdge)
            SetInterrupt(InterruptCb1);
    }

    private byte ReadPortA(bool clearHandshakeFlags)
    {
        if (clearHandshakeFlags)
            ClearInterrupt((byte)(InterruptCa1 | InterruptCa2));
        return (byte)((_portA & _ddrA) | (_readPortAInput() & ~_ddrA));
    }

    private byte ReadPortB()
    {
        ClearInterrupt((byte)(InterruptCb1 | InterruptCb2));
        return (byte)((_portB & _ddrB) | (_readPortBInput() & ~_ddrB));
    }

    private void WritePortA(byte value, bool clearHandshakeFlags)
    {
        _portA = value;
        if (clearHandshakeFlags)
            ClearInterrupt((byte)(InterruptCa1 | InterruptCa2));
        _writePortAOutput(value);
    }

    private void WritePortB(byte value)
    {
        _portB = value;
        ClearInterrupt((byte)(InterruptCb1 | InterruptCb2));
        _writePortBOutput(value);
    }

    private byte ReadTimer1Low()
    {
        ClearInterrupt(InterruptTimer1);
        return (byte)_timer1Counter;
    }

    private byte ReadTimer2Low()
    {
        ClearInterrupt(InterruptTimer2);
        return (byte)_timer2Counter;
    }

    private void WritePeripheralControl(byte value)
    {
        _pcr = value;
        ApplyControlOutput((value >> 1) & 0x07, isCa2: true);
        ApplyControlOutput((value >> 5) & 0x07, isCa2: false);
    }

    private void ApplyControlOutput(int mode, bool isCa2)
    {
        if (mode is not (6 or 7))
            return;
        var level = mode == 7;
        if (isCa2)
        {
            _ca2 = level;
            _writeCa2(level);
        }
        else
        {
            _cb2 = level;
            _writeCb2(level);
        }
    }

    private void TickTimer1(int cycles)
    {
        if (!_timer1Running)
            return;

        var remaining = cycles;
        while (_timer1Running && remaining > _timer1Counter)
        {
            remaining -= _timer1Counter + 1;
            SetInterrupt(InterruptTimer1);
            if ((_acr & 0x40) != 0)
                _timer1Counter = _timer1Latch;
            else
                _timer1Running = false;
        }
        if (_timer1Running)
            _timer1Counter -= (ushort)remaining;
    }

    private void TickTimer2(int cycles)
    {
        if (!_timer2Running || (_acr & 0x20) != 0)
            return;

        if (cycles > _timer2Counter)
        {
            _timer2Running = false;
            SetInterrupt(InterruptTimer2);
        }
        else
        {
            _timer2Counter -= (ushort)cycles;
        }
    }

    private void SetInterrupt(byte mask)
    {
        _ifr |= mask;
        UpdateIrq();
    }

    private void ClearInterrupt(byte mask)
    {
        _ifr &= (byte)~mask;
        UpdateIrq();
    }

    private void UpdateIrq() => _irqChanged(IrqActive);
}
