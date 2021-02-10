namespace Highbyte.DotNet6502
{
    public static class BinaryArithmeticHelpers
    {
        /// <summary>
        /// Adds two byte values (unsigned or signed).
        /// 
        /// Uses ProcessorStatus.Carry flag as input to calculation, and sets it after calculation as appropriate.
        /// 
        /// Also sets ProcessorStatus.Overflow flag after calculation if 
        ///     - value1 and value2 both had sign bit (7) clear, but added result has sign bit set.
        ///     - value1 and value2 both had sign bit (7) set ,  but added result has sign bit clear.
        /// </summary>
        /// <param name="value1"></param>
        /// <param name="value"></param>
        /// <param name="PC"></param>
        /// <returns></returns>
        public static byte AddWithCarryAndOverflow(byte value1, byte value2, ProcessorStatus processorStatus)
        {
            // Perform add with carry
            ushort sum = (ushort) (value1 + value2 + (byte)(processorStatus.Carry?1:0));
            byte result = (byte)(sum & 0xff);

            // Set carry if there was a overflow when adding two bytes
            processorStatus.Carry = (byte)(sum>>8) == 1;
            processorStatus.Zero = result == 0;
            processorStatus.Negative = result.IsBitSet(7);

            // Overflow flag should be set in these situations
            //    If both original values were signed and negative (bit 7 set), but the result was not negative (bit 7 clear)
            // or If both original values were signed and positive (bit 7 clear), but the result was negative (bit 7 set)
            processorStatus.Overflow = (   (value1.IsBitSet(7) && value2.IsBitSet(7) && !result.IsBitSet(7))
                                        || (!value1.IsBitSet(7) && !value2.IsBitSet(7) && result.IsBitSet(7)));

            return result;
        }


            // Add with carry examples
            //            
            // Ex 1:
            // Carry: 0
            // 0000 0001
            // 0000 0001
            // ---------
            // 0000 0010
            // Carry: 0

            // Ex 1:
            // Carry: 1
            // 0000 0001
            // 0000 0001
            // ---------
            // 0000 0011
            // Carry: 0

            // Ex 3:
            // Carry: 0
            // 1111 1111
            // 0000 0001
            // ---------
            // 0000 0001
            // Carry: 1

            // Ex 3:
            // Carry: 0
            // 1111 1111
            // 0000 0001
            // ---------
            // 0000 0001
            // Carry: 1

            // Ex 4:
            // Carry: 0
            // C: 1111 11  
            // V1:0100 0010
            // v2:1111 1111
            //    ---------
            // R: 0100 0001
            // Carry: 1

            // Ex: 5
            // Carry: 0
            // C: 
            // V1 0000 0001    (1)
            // V2 1100 1110    (-50)
            //    ---------
            // R: 1100 1111    (-49)
            // Carry: 0

            // Ex: 6
            // Carry: 0
            // C: 1111 11
            // V1 0000 0010    (2)
            // V2 1111 1111    (-1)
            //    ---------
            // R: 0000 0001    (+1)
            // Carry: 1  (Why Carry when we haven't exceeded -128 to +127 number space for the result (1)? 
            //            The algorithm above also sets 1, and other emulators also sets it to 1, )


        public static byte SubtractWithCarryAndOverflow(byte value1, byte value2, ProcessorStatus processorStatus)
        {
            byte invertedValue2 = (byte)~value2; 
            return AddWithCarryAndOverflow(value1, invertedValue2, processorStatus);
        }

        public static void SetFlagsAfterRegisterLoadIncDec(byte register, ProcessorStatus processorStatus)
        {
            processorStatus.Zero = register == 0x00;
            processorStatus.Negative = register.IsBitSet(7);
        }

        /// <summary>
        /// Compare (CPM, CPY, CPX) are always ***Unsigned*** comparsion.
        /// - Carry:    Register >= Value.
        /// - Zero:     Register == Value
        /// - Negative: Bit 7 set in (Register-Value)
        /// 
        /// Ex:
        ///  Register:  130 (0x82) - If it was treated as signed would be -126...
        ///  Value:     26
        ///  Carry:     1           - Because 130 >= 26. If the values were treated as signed (which it's not!), carry would have been 0.
        ///  Zero:      0
        ///  Negative:  0           - Because bit 7 in (130-26) is not set
        /// </summary>
        /// <param name="register"></param>
        /// <param name="value"></param>
        /// <param name="processorStatus"></param>
        public static void SetFlagsAfterCompare(byte register, byte value, ProcessorStatus processorStatus)
        {
            processorStatus.Carry = register >= value;
            processorStatus.Zero = register == value;
            byte regMinusValue = (byte)(register - value);
            processorStatus.Negative = ((byte)regMinusValue).IsBitSet(7);
        }

        public static byte PerformASLAndSetStatusRegisters(byte register, ProcessorStatus processorStatus)
        {
            processorStatus.Carry = register.IsBitSet(7);
            var shiftedRegister = (byte)(register << 1);
            SetFlagsAfterRegisterLoadIncDec(shiftedRegister, processorStatus);
            return shiftedRegister;
        }

        public static byte PerformLSRAndSetStatusRegisters(byte register, ProcessorStatus processorStatus)
        {
            processorStatus.Carry = register.IsBitSet(0);
            var shiftedRegister = (byte)(register >> 1);
            SetFlagsAfterRegisterLoadIncDec(shiftedRegister, processorStatus);
            return shiftedRegister;
        }

        public static byte PerformROLAndSetStatusRegisters(byte register, ProcessorStatus processorStatus)
        {
            bool originalCarry = processorStatus.Carry;
            processorStatus.Carry = register.IsBitSet(7);
            var shiftedRegister = (byte)(register << 1);
            shiftedRegister.ChangeBit(0, originalCarry);
            SetFlagsAfterRegisterLoadIncDec(shiftedRegister, processorStatus);
            return shiftedRegister;
        }
        public static byte PerformRORAndSetStatusRegisters(byte register, ProcessorStatus processorStatus)
        {
            bool originalCarry = processorStatus.Carry;
            processorStatus.Carry = register.IsBitSet(0);
            var shiftedRegister = (byte)(register >> 1);
            shiftedRegister.ChangeBit(7, originalCarry);
            SetFlagsAfterRegisterLoadIncDec(shiftedRegister, processorStatus);
            return shiftedRegister;
        }                

        public static void PerformBITAndSetStatusRegisters(byte register, byte memoryValue, ProcessorStatus processorStatus)
        {
            processorStatus.Zero = (register & memoryValue) == 0;
            processorStatus.Overflow = memoryValue.IsBitSet(StatusFlagBits.Overflow);
            processorStatus.Negative = memoryValue.IsBitSet(StatusFlagBits.Negative);
        }
    }
}
