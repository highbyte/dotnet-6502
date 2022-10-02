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

        public void KeyPressed(int keyCode)
        {
            var found = TEMP_ASCIICodeToPETSCII(keyCode, out byte petsciiCode);
            if(!found)
                return;
            if(BufferIndex >= BUFFER_MAX_LENGTH)
                return;
            Buffer[BufferIndex++] = petsciiCode;
        }

        private static bool TEMP_ASCIICodeToPETSCII(int asciiCode, out byte petsciiCode)
        {
            bool found = true;
            // If the ASCII character is a-z, make it A-Z
            if(asciiCode >= 97-32 && asciiCode <= 122-32)
                petsciiCode = (byte)(asciiCode + 32);
            // If the ASCII character is A-Z, make it a-z
            else if(asciiCode >= 65+32 && asciiCode <= 90+32)
                petsciiCode = (byte)(asciiCode - 32);
            else
            {
                petsciiCode = (byte)asciiCode;
                //petsciiCode = 0;
                //found = false;
            }
            return found;
        }
    }
}