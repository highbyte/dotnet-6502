using System;
using System.Collections.Generic;
using System.Linq;

namespace Highbyte.DotNet6502
{
    public class Memory
    {
        public const uint MAX_MEMORY_SIZE = 1024*64; // 65536 / 64KB (0x0000 - 0xffff)

        // Segment 0 is always required
        public const uint SEGMENT_0_SIZE = 1024*8; // 8192 /  8KB  (0x0000 - 0x1fff)

        // Additional segments (TODO: Could be configurable?)
        public const uint ADDITIONAL_SEGMENT_SIZE = 1024*8; // 8192 /  8KB  (0x2000 - <=0xffff)

        private readonly List<MemorySegment> _memorySegments;
        public List<MemorySegment> MemorySegments => _memorySegments;

        public byte[] Data { get => TotalMemoryFromAllSegments();}

        public uint Size => TotalMemoryLengthFromAllSegments();
        
        public byte this[ushort index] 
        {
            get
            {
                var bank = GetSegmentAndOffsetFromMemoryAddress(index, out ushort bankOffset);
                return _memorySegments[bank][bankOffset];
            }
            set
            {
                var bank = GetSegmentAndOffsetFromMemoryAddress(index, out ushort bankOffset);
                _memorySegments[bank][bankOffset] = value;
            }
        }

        public Memory(): this(MAX_MEMORY_SIZE)
        {
        }

        public Memory(uint memorySize)
        {
            if(memorySize<SEGMENT_0_SIZE)
                throw new ArgumentException($"The specified memorySize {memorySize} is less than minimum allowed memory size {SEGMENT_0_SIZE}", nameof(memorySize));

            if(memorySize>MAX_MEMORY_SIZE)
                throw new ArgumentException($"The specified memorySize {memorySize} is greater than maximum allowed memory size {MAX_MEMORY_SIZE}", nameof(memorySize));

            if((memorySize - SEGMENT_0_SIZE) % ADDITIONAL_SEGMENT_SIZE !=0)
                throw new ArgumentException($"The size of the memory above the required minimum {SEGMENT_0_SIZE} must be evenly divisible by the bank size {ADDITIONAL_SEGMENT_SIZE}", nameof(memorySize));


            // Add required segment 0 with a minimum size, not changable.
            ushort segmentStartAddess = 0x0000;
            _memorySegments = new List<MemorySegment>
            {
                new MemorySegment(segmentStartAddess, SEGMENT_0_SIZE)
            };

            // Create additional segments until we reach required total memory size
            var noAdditionalBanks = (memorySize - SEGMENT_0_SIZE) / ADDITIONAL_SEGMENT_SIZE;
            for (byte i = 0; i < noAdditionalBanks; i++)
            {
                segmentStartAddess += (ushort) _memorySegments[i].Size; // Adds previous segments size (i=0 already added above)
                _memorySegments.Add(new MemorySegment(segmentStartAddess, ADDITIONAL_SEGMENT_SIZE));
            }

        }

        public Memory(List<MemorySegment> memorySegments)
        {
            _memorySegments = memorySegments;
        }

        private byte[] TotalMemoryFromAllSegments()
        {
            byte[] allMemory = new byte[TotalMemoryLengthFromAllSegments()];
            int offset = 0;
            foreach (byte[] data in _memorySegments.Select(x=>x.Memory))
            {
                Buffer.BlockCopy(data, 0, allMemory, offset, data.Length);
                offset += data.Length;
            }
            return allMemory;
        }

        private uint TotalMemoryLengthFromAllSegments()
        {
            return (uint)_memorySegments.Sum(x => x.Size);
        }        

        private byte GetSegmentAndOffsetFromMemoryAddress(ushort address, out ushort bankOffset)
        {
            if(address<SEGMENT_0_SIZE)
            {
                bankOffset = address;
                return 0;
            }
            var additionalBankNo = Math.DivRem((int)(address - SEGMENT_0_SIZE), (int)ADDITIONAL_SEGMENT_SIZE, out int remainder);
            bankOffset = (ushort)remainder;
            return (byte) (additionalBankNo + 1) ;
        }

        public void ChangeCurrentSegmentBank(byte segmentNumber, byte segmentBankId)
        {
            if(segmentNumber == 0)
                throw new ArgumentException($"Segment 0 can not be changed.", nameof(segmentNumber));
            if(segmentNumber >= _memorySegments.Count)
                throw new ArgumentException($"Maximum segmentNumber is {_memorySegments.Count-1}", nameof(segmentNumber));
            _memorySegments[segmentNumber].ChangeCurrentSegmentBank(segmentBankId);
        }

        /// <summary>
        /// Sets a new empty bank (all memory locations with value 0) for specified segmentNumber and segmentBankId.
        /// </summary>
        /// <param name="segmentNumber"></param>
        /// <param name="segmentBankId"></param>
        public void AddMemorySegmentBank(byte segmentNumber)
        {
            AddMemorySegmentBank(segmentNumber,  new byte[ADDITIONAL_SEGMENT_SIZE]);
        }

        /// <summary>
        /// Adds a bank to the specified the specified segmentNumber with the specified memory array.
        /// SegmentNumber 0 not allowed to use (it's the required first X bytes of memory)
        /// </summary>
        /// <param name="segmentNumber">The segment number that should be configured.</param>
        /// <param name="segmentBankContent">The memory byte array for the new bank in segment specified by segmentNumber</param>
        public void AddMemorySegmentBank(byte segmentNumber, byte[] segmentBankContent)
        {
            if(segmentNumber == 0)
                throw new ArgumentException($"Segment 0 can not have multiple memory banks.", nameof(segmentNumber));
            if(segmentNumber >= _memorySegments.Count)
                throw new ArgumentException($"Maximum segmentNumber is {_memorySegments.Count-1}", nameof(segmentNumber));

            if(segmentBankContent.Length != ADDITIONAL_SEGMENT_SIZE)
                throw new ArgumentException($"The memory size for a segment bank must be {ADDITIONAL_SEGMENT_SIZE}.", nameof(segmentBankContent));

            _memorySegments[segmentNumber].AddSegmentBank(segmentBankContent);
        }

        /// <summary>
        /// Updates the specified segmentNumber and segmentBankId with a memory array.
        /// SegmentNumber 0 not allowed to use (it's the required first X bytes of memory)
        /// SegmentBankId 0 not allowed to use (it's the original memory)
        /// </summary>
        /// <param name="segmentNumber">The segment number that should be configured.</param>
        /// <param name="segmentBankId">The segment bank id within the segment (unique per segment).</param>
        /// <param name="segmentBankContent">The memory byte array for the segmentNumber and segmentBankId.</param>
        public void UpdateMemorySegmentBank(byte segmentNumber, byte segmentBankId, byte[] segmentBankContent)
        {
            if(segmentNumber == 0)
                throw new ArgumentException($"Segment 0 can not be changed.", nameof(segmentNumber));
            if(segmentNumber >= _memorySegments.Count)
                throw new ArgumentException($"Maximum segmentNumber is {_memorySegments.Count-1}", nameof(segmentNumber));

            if(segmentBankContent.Length != ADDITIONAL_SEGMENT_SIZE)
                throw new ArgumentException($"The memory size for a segment bank must be {ADDITIONAL_SEGMENT_SIZE}.", nameof(segmentBankContent));

            _memorySegments[segmentNumber].UpdateSegmentBank(segmentBankId, segmentBankContent);
        }        

        public void StoreData(ushort address, byte[] data)
        {
            if((address + data.Length) > MAX_MEMORY_SIZE)
                throw new DotNet6502Exception($"Address {address} + size of data {data.Length} exceeds maximum memory limit {MAX_MEMORY_SIZE}");

            for (int i = 0; i < data.Length; i++)
            {
                this[(ushort)(address+i)] = data[i];  
            }
        }

        public byte[] ReadData(ushort address, ushort length)
        {
            if((address + length) > MAX_MEMORY_SIZE)
                throw new DotNet6502Exception($"Address {address} + length {length} exceeds maximum memory limit {MAX_MEMORY_SIZE}");

            byte[] readArray = new byte[length];
            for (int i = 0; i < length; i++)
            {
                readArray[i] = this[(ushort)(address+i)];
            }
            return readArray;
        }        

        public bool IsBitSet(ushort address, int bit)
        {
            var value = this[address];
            return value.IsBitSet(bit);
        }

        public void SetBit(ushort address, int bit)
        {
            ChangeBit(address, bit, true);
        }

        public void ClearBit(ushort address, int bit)
        {
            ChangeBit(address, bit, false);
        }

        public void ChangeBit(ushort address, int bit, bool state)
        {
            var value = this[address];
            value.ChangeBit(bit, state);
            this[address] = value;            
        }

        public Memory Clone()
        {
            var memoryClone = new Memory(_memorySegments);
            return memoryClone;
        }
    }
}
