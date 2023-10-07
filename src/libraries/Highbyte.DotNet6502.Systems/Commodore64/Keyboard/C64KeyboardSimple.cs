//namespace Highbyte.DotNet6502.Systems.Commodore64.Keyboard;

//public class C64KeyboardSimple
//{
//    private const int BUFFER_MAX_LENGTH = 10;

//    public byte BufferIndex { get; private set; }
//    public byte[] Buffer { get; private set; }
//    public byte StopKeyFlag { get; set; }


//    public void MapIOLocations(Memory c64Mem)
//    {
//        // Trivial Keyboard implementation, skip the C64 Keyboard matrix and inject keypresses directly into the keyboard buffer (normally done by Kernal in Basic)
//        // Address: 0x00c6: Keyboard buffer index
//        c64Mem.MapReader(0x00c6, BufferIndexLoad);
//        c64Mem.MapWriter(0x00c6, BufferIndexStore);
//        // Address: 0x0277 - 0x0280: Keyboard buffer
//        c64Mem.MapRAM(0x0277, Buffer);
//        // Address: 0x0091: Stop key flag
//        c64Mem.MapReader(0x0091, StopKeyFlagLoad);
//        c64Mem.MapWriter(0x0091, StopKeyFlagStore);
//    }

//    public void BufferIndexStore(ushort _, byte value)
//    {
//        BufferIndex = value;
//    }
//    public byte BufferIndexLoad(ushort _)
//    {
//        return BufferIndex;
//    }

//    public void StopKeyFlagStore(ushort _, byte value)
//    {
//        StopKeyFlag = value;
//    }
//    public byte StopKeyFlagLoad(ushort _)
//    {
//        return StopKeyFlag;
//    }

//    public C64KeyboardSimple()
//    {
//        BufferIndex = 0;
//        Buffer = new byte[BUFFER_MAX_LENGTH];
//    }

//    public void KeyPressed(byte petsciiCode)
//    {
//        if (BufferIndex >= BUFFER_MAX_LENGTH)
//            return;
//        Buffer[BufferIndex++] = petsciiCode;
//    }
//}
