namespace Highbyte.DotNet6502
{
    public class MemorySegmentBank
    {

        private readonly byte[] _memory;
        public byte[] Memory => _memory;

        public uint Size => (uint)_memory.Length;

        public MemorySegmentBank(uint segmentSize)
        {
            _memory = new byte[segmentSize];
        }

        public MemorySegmentBank(byte[] memory)
        {
            _memory = memory;
        }

        public MemorySegmentBank Clone()
        {
            var memorySegmentBankClone = new MemorySegmentBank(_memory);
            return memorySegmentBankClone;
        }
    }
}