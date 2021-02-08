namespace Highbyte.DotNet6502
{
    public class ProcessorStatus
    {
        private byte _value;
        /// <summary>
        /// The status register value as byte
        /// </summary>
        /// <value></value>
        public byte Value { get => _value; set => _value = value; }

        /// <summary>
        /// The carry flag is set if the last operation caused an overflow from bit 7 of the result or an underflow from bit 0. This condition is set during arithmetic, comparison and during logical shifts. It can be explicitly set using the 'Set Carry Flag' (SEC) instruction and cleared with 'Clear Carry Flag' (CLC).
        /// </summary>
        public bool Carry
        {
            get { return _value.IsBitSet(StatusFlagBits.Carry); }
            set { _value.ChangeBit(StatusFlagBits.Carry, value); }
        }

        /// <summary>
        /// The zero flag is set if the result of the last operation as was zero.
        /// </summary>
        public bool Zero
        {
            get { return _value.IsBitSet(StatusFlagBits.Zero); }
            set { _value.ChangeBit(StatusFlagBits.Zero, value); }
        }

        /// <summary>
        /// The interrupt disable flag is set if the program has executed a 'Set Interrupt Disable' (SEI) instruction. While this flag is set the processor will not respond to interrupts from devices until it is cleared by a 'Clear Interrupt Disable' (CLI) instruction.
        /// </summary>
        public bool InterruptDisable
        {
            get { return _value.IsBitSet(StatusFlagBits.InterruptDisable); }
            set { _value.ChangeBit(StatusFlagBits.InterruptDisable, value); }
        }
        
        /// <summary>
        /// While the decimal mode flag is set the processor will obey the rules of Binary Coded Decimal (BCD) arithmetic during addition and subtraction. The flag can be explicitly set using 'Set Decimal Flag' (SED) and cleared with 'Clear Decimal Flag' (CLD).
        /// </summary>
        public bool Decimal
        {
            get { return _value.IsBitSet(StatusFlagBits.Decimal); }
            set { _value.ChangeBit(StatusFlagBits.Decimal, value); }
        }

        /// <summary>
        /// The break command bit is set when a BRK instruction has been executed and an interrupt has been generated to process it.
        /// </summary>
        public bool Break
        {
            get { return _value.IsBitSet(StatusFlagBits.Break); }
            set { _value.ChangeBit(StatusFlagBits.Break, value); }
        }

        /// <summary>
        /// The Unused or un-documented bit
        /// </summary>
        public bool Unused
        {
            get { return _value.IsBitSet(StatusFlagBits.Unused); }
            set { _value.ChangeBit(StatusFlagBits.Unused, value); }
        }

        /// <summary>
        /// The overflow flag is set during arithmetic operations if the result has yielded an invalid 2's complement result (e.g. adding to positive numbers and ending up with a negative result: 64 + 64 => -128). It is determined by looking at the carry between bits 6 and 7 and between bit 7 and the carry flag.
        /// </summary>
        public bool Overflow
        {
            get { return _value.IsBitSet(StatusFlagBits.Overflow); }
            set { _value.ChangeBit(StatusFlagBits.Overflow, value); }
        }

        /// <summary>
        /// The negative flag is set if the result of the last operation had bit 7 set to a one.
        /// </summary>
        public bool Negative
        {
            get { return _value.IsBitSet(StatusFlagBits.Negative); }
            set { _value.ChangeBit(StatusFlagBits.Negative, value); }
        }

        public ProcessorStatus()
        {
            Carry = false;
            Zero = false;
            InterruptDisable = false;
            Decimal = false;
            Break = false;
            Unused = false;
            Overflow = false;
            Negative = false;
        }
        public ProcessorStatus(byte value)
        {
            _value = value;
        }

        public ProcessorStatus Clone()
        {
            return new ProcessorStatus(Value);
        }
    }

    public enum StatusFlagBits
    {
        Carry = 0,
        Zero = 1,
        InterruptDisable = 2,
        Decimal = 3,
        Break = 4, // Sometimes called B flag?
        Unused = 5, // Undocumented but used by CPU in some instructions
        Overflow = 6,
        Negative = 7
    }
}
