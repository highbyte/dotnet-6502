namespace Highbyte.DotNet6502.Systems.Commodore64
{
    public class Keyboard
    {
        private const int BUFFER_MAX_LENGTH = 10;

        public byte BufferIndex { get; private set; }
        public byte[] Buffer { get; private set; }
        public byte StopKeyFlag { get; set; }

        public void BufferIndexStore(ushort _, byte value)
        {
            BufferIndex = value;
        }
        public byte BufferIndexLoad(ushort _)
        {
            return BufferIndex;
        }

        public void StopKeyFlagStore(ushort _, byte value)
        {
            StopKeyFlag = value;
        }
        public byte StopKeyFlagLoad(ushort _)
        {
            return StopKeyFlag;
        }

        public Keyboard()
        {
            BufferIndex = 0;
            Buffer = new byte[BUFFER_MAX_LENGTH];
        }

        public void KeyPressed(byte petsciiCode)
        {
            if(BufferIndex >= BUFFER_MAX_LENGTH)
                return;
            Buffer[BufferIndex++] = petsciiCode;
        }
    }
}