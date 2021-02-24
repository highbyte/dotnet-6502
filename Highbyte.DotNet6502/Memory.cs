using System;
using System.Collections.Generic;
using System.Linq;

namespace Highbyte.DotNet6502
{
    public class Memory
    {
        public const uint MAX_MEMORY_SIZE = 1024*64; // 65536 / 64KB (0x0000 - 0xffff)

        // Segment 0 is always required
        public const uint SEGMENT_0_SIZE = 1024*8;      // 8192 /  8KB  (0x0000 - 0x1fff)
        // Additional memory 
        public const uint ADDITIONAL_SEGMENT_SIZE = 1024*8;   // 8192 /  8KB  (0x2000 - <=0xffff)

        private readonly Dictionary<byte, byte[]> _memorySegments;
        public Dictionary<byte, byte[]> MemorySegments => _memorySegments;

        // TODO: Could memory segment banks use span<> to be more efficient when changing banks, like to requiring to copy the entire bank contents but rather change a span "pointer"?
        private readonly Dictionary<byte, Dictionary<byte,byte[]>> _memorySegmentBanks;
        
        public Dictionary<byte, Dictionary<byte,byte[]>> MemorySegmentBanks => _memorySegmentBanks;


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


            // Add required segment 0 of 8KB, not changable.
            _memorySegments = new Dictionary<byte, byte[]>
            {
                { 0, new byte[SEGMENT_0_SIZE] }
            };

            // Create additional segments until we reach required total memory size
            var noAdditionalBanks = (memorySize - _memorySegments[0].Length) / ADDITIONAL_SEGMENT_SIZE;
            for (byte i = 0; i < noAdditionalBanks; i++)
            {
                _memorySegments.Add((byte)(i+1), new byte[ADDITIONAL_SEGMENT_SIZE]);
            }

            // Copy the content of the memory segments to segment banks contentblock 0
            _memorySegmentBanks = new Dictionary<byte, Dictionary<byte, byte[]>>();
            for (byte i = 0; i < _memorySegments.Count; i++)
            {
                var bankContentBlock = new Dictionary<byte, byte[]>
                {
                    { 0, _memorySegments[i] }
                };
                _memorySegmentBanks.Add(i, bankContentBlock);
            }
        }

        public Memory(Dictionary<byte, byte[]> memorySegments, Dictionary<byte, Dictionary<byte,byte[]>> memorySegmentBanks)
        {
            _memorySegments = memorySegments;
            _memorySegmentBanks = memorySegmentBanks;
        }

        private byte[] TotalMemoryFromAllSegments()
        {
            byte[] allMemory = new byte[TotalMemoryLengthFromAllSegments()];
            int offset = 0;
            foreach (byte[] data in _memorySegments.Values)
            {
                Buffer.BlockCopy(data, 0, allMemory, offset, data.Length);
                offset += data.Length;
            }
            return allMemory;
        }

        private uint TotalMemoryLengthFromAllSegments()
        {
            return (uint)_memorySegments.Sum(x => x.Value.Length);
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

        public void ChangeSegmentBank(byte segmentNumber, byte segmentBankId)
        {
            if(segmentNumber == 0)
                throw new ArgumentException($"Segment 0 can not be changed.", nameof(segmentNumber));
            if(segmentNumber > _memorySegments.Count)
                throw new ArgumentException($"Maximum segmentNumber is {_memorySegments.Count}", nameof(segmentNumber));
            if(!_memorySegmentBanks.ContainsKey(segmentNumber))
                throw new ArgumentException($"Segment banks for segment {segmentNumber} missing", nameof(segmentNumber));
            if(!_memorySegmentBanks[segmentNumber].ContainsKey(segmentBankId))
                throw new ArgumentException($"Segment {segmentNumber} has no bank with segmentBankId {segmentBankId}", nameof(segmentBankId));

            _memorySegments[segmentNumber] = _memorySegmentBanks[segmentNumber][segmentBankId];
        }


        /// <summary>
        /// Sets a new empty bank (all memory locations with value 0) for specified segmentNumber and segmentBankId.
        /// </summary>
        /// <param name="segmentNumber"></param>
        /// <param name="segmentBankId"></param>
        public void ConfigureMemorySegmentBank(byte segmentNumber, byte segmentBankId)
        {
            ConfigureMemorySegmentBank(segmentNumber, segmentBankId, new byte[ADDITIONAL_SEGMENT_SIZE]);
        }

        /// <summary>
        /// Configures the specified segmentNumber and segmentBankId with a memory array.
        /// SegmentNumber 0 not allowed to use (it's the required first X bytes of memory)
        /// SegmentBankId 0 not allowed to use (it's the original memory)
        /// </summary>
        /// <param name="segmentNumber">The segment number that should be configured.</param>
        /// <param name="segmentBankId">The segment bank id within the segment (unique per segment).</param>
        /// <param name="segmentBankContent">The memory byte array for the segmentNumber and segmentBankId.</param>
        public void ConfigureMemorySegmentBank(byte segmentNumber, byte segmentBankId, byte[] segmentBankContent)
        {
            if(segmentNumber == 0)
                throw new ArgumentException($"Segment 0 can not be changed.", nameof(segmentNumber));
            if(segmentNumber >= _memorySegments.Count)
                throw new ArgumentException($"Segment {segmentNumber} does not exist.", nameof(segmentNumber));
            if(segmentBankId == 0)
                throw new ArgumentException($"SegmentBankId 0 cannot be changed.", nameof(segmentBankId));

            if(segmentBankContent.Length != ADDITIONAL_SEGMENT_SIZE)
                throw new ArgumentException($"The memory size for the segment bank must be {ADDITIONAL_SEGMENT_SIZE}.", nameof(segmentBankContent));

            if(_memorySegmentBanks[segmentNumber].ContainsKey(segmentBankId))
                _memorySegmentBanks[segmentNumber][segmentBankId] = segmentBankContent;
            else
                _memorySegmentBanks[segmentNumber].Add(segmentBankId, segmentBankContent);
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
            var memoryClone = new Memory(_memorySegments, _memorySegmentBanks);
            return memoryClone;
        }
    }
}
