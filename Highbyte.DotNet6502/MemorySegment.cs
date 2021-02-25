using System;
using System.Collections.Generic;

namespace Highbyte.DotNet6502
{
    public class MemorySegment
    {

        private ushort _startAddress;
        public ushort StartAddress => _startAddress;
        
        private readonly uint _size;
        public uint Size => _size;

        private readonly List<MemorySegmentBank> _banks;
        public List<MemorySegmentBank> Banks => _banks;

        private byte _currentBankNo;

        public byte this[ushort index] 
        {
            get
            {
                return _banks[_currentBankNo].Memory[index];
            }
            set
            {
               _banks[_currentBankNo].Memory[index] = value;
            }
        }

        public byte[] Memory => _banks[_currentBankNo].Memory;

        public MemorySegment(ushort startAddress, uint segmentSize)
        {
            _startAddress = startAddress;
            _size = segmentSize;

            // All memory segments has a bank 0, the first in the list.
            _currentBankNo = 0;
            _banks = new List<MemorySegmentBank>{new MemorySegmentBank(segmentSize)};
        }

        public MemorySegment(ushort startAddress, List<MemorySegmentBank> banks, byte currentBankNo)
        {
            _startAddress = startAddress;
            _banks = banks;
            _currentBankNo = currentBankNo;
        }

        public void ChangeCurrentSegmentBank(byte segmentBankId)
        {
            if(segmentBankId >= _banks.Count )
                throw new ArgumentException($"Maximum segmentBankId is {_banks.Count-1}", nameof(segmentBankId));
            _currentBankNo = segmentBankId;
        }

        public void AddSegmentBank()
        {
            Banks.Add(new MemorySegmentBank(Size));
        }        

        public void AddSegmentBank(byte[] memory)
        {
            if(memory.Length != Size)
                throw new ArgumentException($"Segment bank must be the same size as the segment is configured for: {Size} bytes", nameof(memory));
            Banks.Add(new MemorySegmentBank(memory));
        }        

        public void UpdateSegmentBank(byte segmentBankId, byte[] memory)
        {
            if(segmentBankId >= _banks.Count)
                throw new ArgumentException($"Maximum segmentBankId is {_banks.Count-1}", nameof(segmentBankId));

            if(memory.Length != Size)
                throw new ArgumentException($"Segment bank must be the same size as the segment is configured for: {Size} bytes", nameof(memory));
            Banks[segmentBankId] = new MemorySegmentBank(memory);
        }        

        public MemorySegment Clone()
        {
            var memorySegmentClone = new MemorySegment(_startAddress, _banks, _currentBankNo);
            return memorySegmentClone;
        }
    }
}
