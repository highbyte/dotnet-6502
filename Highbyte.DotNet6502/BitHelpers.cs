using System;

namespace Highbyte.DotNet6502
{
    public static class BitHelpers
    {
        public static bool IsBitSet(this byte data, int bit)
        {
            return ((data >> bit) & 1) == 1; 
        }
        
        public static bool IsBitSet(this byte data, StatusFlagBits bit)
        {
            return ((data >> (int)bit) & 1) == 1; 
        }

        public static void SetBit(this ref byte data, int bit)
        {
            ChangeBit(ref data, bit, true);
        }
        public static void SetBit(this ref byte data, StatusFlagBits bit)
        {
            ChangeBit(ref data, bit, true);
        }
        public static void ClearBit(this ref byte data, int bit)
        {
            ChangeBit(ref data, bit, false);
        }
        public static void ClearBit(this ref byte data, StatusFlagBits bit)
        {
            ChangeBit(ref data, bit, false);
        }        

        public static void ChangeBit(this ref byte data, int bit, bool state)
        {
            byte bitValue = (byte) (1 << bit);
            if(state)
                // Set bit
                data = (byte) (data | bitValue);  // or
            else 
                // Clear bit. TODO: Better way to clear a bit?
                data = (byte) (data & (255 ^ bitValue));  // and + xor
        }

        public static void ChangeBit(this ref byte data, StatusFlagBits bit, bool state)
        {
            byte bitValue = (byte) (1 << (int)bit);
            if(state)
                // Set bit
                data = (byte) (data | bitValue);  // or
            else 
                // Clear bit. TODO: Better way to clear a bit?
                data = (byte) (data & (255 ^ bitValue));  // and + xor
        }     
   }
}