using System.Buffers.Binary;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

namespace Highbyte.DotNet6502.Systems.Commodore64.Input;

/// <summary>Resolved machine input stored by the c64-input snapshot module.</summary>
internal static class C64ResolvedInput
{
    private const int Port1 = sizeof(ulong);
    private const int Port2 = Port1 + 1;
    private const int Flags = Port2 + 1;
    private const int Size = Flags + 1;

    internal static byte[] Capture(Cia1 cia)
    {
        var bytes = new byte[Size];
        var (keys, flags) = cia.Keyboard.CaptureResolvedKeys();
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, keys);
        bytes[Port1] = cia.Joystick.CapturePort(1);
        bytes[Port2] = cia.Joystick.CapturePort(2);
        bytes[Flags] = (byte)flags;
        return bytes;
    }

    internal static void Validate(ReadOnlySpan<byte> bytes)
    {
        const byte portMask = 0x1f;
        const C64KeyboardFlags known = C64KeyboardFlags.RestoreHeld | C64KeyboardFlags.CapsLock
            | C64KeyboardFlags.RunStopHeld | C64KeyboardFlags.RestoreEdge;
        if (bytes.Length != Size || (bytes[Port1] & ~portMask) != 0 || (bytes[Port2] & ~portMask) != 0)
            throw new InvalidDataException("Invalid C64 resolved keyboard/joystick input.");

        var flags = (C64KeyboardFlags)bytes[Flags];
        if ((flags & ~known) != 0 || ((flags & C64KeyboardFlags.RestoreEdge) != 0 && (flags & C64KeyboardFlags.RestoreHeld) == 0))
            throw new InvalidDataException("Invalid C64 resolved keyboard/joystick input.");
    }

    internal static void Restore(Cia1 cia, ReadOnlySpan<byte> bytes)
    {
        Validate(bytes);
        cia.Keyboard.RestoreResolvedKeys(BinaryPrimitives.ReadUInt64LittleEndian(bytes), (C64KeyboardFlags)bytes[Flags]);
        cia.Joystick.RestorePort(1, bytes[Port1]);
        cia.Joystick.RestorePort(2, bytes[Port2]);
    }
}
