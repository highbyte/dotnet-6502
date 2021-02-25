using System;
using System.Collections.Generic;

namespace Highbyte.DotNet6502
{
    public class MemorySegment
    {

        private readonly ushort _startAddress;
        public ushort StartAddress => _startAddress;
        
        private readonly uint _size;
        public uint Size => _size;

        private readonly List<MemorySegmentBank> _banks;
        public List<MemorySegmentBank> Banks => _banks;

        private byte _currentBankNumber;
        public byte CurrentBankNumber => _currentBankNumber;

        public byte[] Memory => _banks[_currentBankNumber].Memory;

        public MemorySegment(ushort startAddress, uint segmentSize)
        {
            _startAddress = startAddress;
            _size = segmentSize;

            // All memory segments has a bank 0, the first in the list.
            _currentBankNumber = 0;
            _banks = new List<MemorySegmentBank>{new MemorySegmentBank(segmentSize)};
        }

        // public MemorySegment(ushort startAddress, List<MemorySegmentBank> banks, byte currentBankNumber)
        // {
        //     _startAddress = startAddress;
        //     _banks = banks;
        //     _currentBankNumber = currentBankNumber;
        // }

        public void ChangeCurrentSegmentBank(byte segmentBankNumber)
        {
            if(segmentBankNumber >= _banks.Count )
                throw new ArgumentException($"Maximum segmentBankNumber is {_banks.Count-1}", nameof(segmentBankNumber));
            _currentBankNumber = segmentBankNumber;
        }   

        public void AddSegmentBank(byte[] memory)
        {
            if(memory.Length != Size)
                throw new ArgumentException($"Segment bank must be the same size as the segment is configured for: {Size} bytes", nameof(memory));
            Banks.Add(new MemorySegmentBank(memory));
        }        

        // public void UpdateSegmentBank(byte segmentBankNumber, byte[] memory)
        // {
        //     if(segmentBankNumber >= _banks.Count)
        //         throw new ArgumentException($"Maximum segmentBankNumber is {_banks.Count-1}", nameof(segmentBankNumber));

        //     if(memory.Length != Size)
        //         throw new ArgumentException($"Segment bank must be the same size as the segment is configured for: {Size} bytes", nameof(memory));
        //     Banks[segmentBankNumber] = new MemorySegmentBank(memory);
        // }        

        // public MemorySegment Clone()
        // {
        //     var memorySegmentClone = new MemorySegment(_startAddress, _banks, _currentBankNumber);
        //     return memorySegmentClone;
        // }
    }
}
