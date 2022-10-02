namespace Highbyte.DotNet6502.Systems.Commodore64.Config
{
    public static class CharacterMaps
    {

        /// <summary>
        /// Maps C64 "PETSCII" codes to ASCII characters
        /// </summary>
        /// <returns></returns>
        // TODO
        // public static Dictionary<byte, byte> PETSCIIMap = new()
        // {
        //     { 0x00, 0x00},
        // };
        
        public static byte C64ScreenCodeToPETSCII(byte screenCode)
        {
            // Ref: http://sta.c64.org/cbm64scrtopet.html
            int petsciiCode = screenCode switch
            {
                >= 0 and <= 31 => screenCode + 64,
                >= 32 and <= 63 => screenCode,
                >= 64 and <= 93 => screenCode + 128,
                94 => 255,
                95 => 223,
                >= 96 and <= 127 => screenCode + 64,
                >= 128 and <= 159 => screenCode - 128,
                >= 160 and <= 191 => screenCode - 128,
                >= 192 and <= 223 => screenCode - 64,
                >= 224 and <= 254 => screenCode - 64,
            };
            return (byte)petsciiCode;
        }

        public static byte PETSCIICodeToASCII(byte petsciiCode)
        {
            // Ref: https://thec64community.online/thread/77/petscii-ascii-tool?page=1&scrollTo=438
            byte asciiCode;
            // If the PETSCII character is A-Z, make it a-z (PETSCII 97-122, subtract 32)
            if(petsciiCode >= 97 && petsciiCode <= 122)
                asciiCode = (byte)(petsciiCode - 32);
            // If the PETSCII character is a-z, make it A-Z (PETSCII 65-90, add 32)
            else if(petsciiCode >= 65 && petsciiCode <= 90)
                asciiCode = (byte)(petsciiCode + 32);
            // If the PETSCII character is 192-223, subtract 96. Then subtract 32 if the resultant value is 97-122.                    
            else if(petsciiCode >= 192 && petsciiCode <= 223)
            {
                asciiCode = (byte)(petsciiCode - 96);
                if(asciiCode >= 97 && asciiCode <= 122)
                    asciiCode = (byte)(asciiCode - 32);
            }
            else
                asciiCode = petsciiCode;
            return asciiCode;
        }
    }
}
