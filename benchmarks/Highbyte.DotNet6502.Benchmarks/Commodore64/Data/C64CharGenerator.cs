using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Benchmarks.Commodore64.Data
{
    public class C64CharGenerator
    {
        private readonly C64 _c64;
        private readonly Vic2 _vic2;
        private readonly Memory _vic2Mem;

        public C64CharGenerator(C64 c64)
        {
            _c64 = c64;
            _vic2 = c64.Vic2;
            _vic2Mem = c64.Mem;
        }

        public void WriteToScreen(byte characterCode, byte col, byte row)
        {
            var screenAddress = (ushort)(_vic2.VideoMatrixBaseAddress + (row * _vic2.Vic2Screen.TextCols) + col);
            _vic2Mem[screenAddress] = characterCode;
        }
        public void CreateCharData()
        {
            foreach (var characterCode in s_chars.Keys)
            {
                var characterSetLineAddress = (ushort)(_vic2.CharsetManager.CharacterSetAddressInVIC2Bank
                                        + (characterCode * _vic2.Vic2Screen.CharacterHeight));
                for (int i = 0; i < s_chars[characterCode].Length; i++)
                {
                    _vic2Mem[(ushort)(characterSetLineAddress + i)] = s_chars[characterCode][i];
                }
            }
        }

        private static Dictionary<int, byte[]> s_chars
        {
            get
            {
                return new Dictionary<int, byte[]>
                {
                    // A
                    {1, new byte[] {
                        0b00011000,
                        0b01100110,
                        0b01111110,
                        0b01100110,
                        0b01100110,
                        0b01100110,
                        0b00000000
                        }
                    }
                };
            }
        }
    }
}
