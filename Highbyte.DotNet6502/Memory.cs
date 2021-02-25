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

        // Additional segments. 
        // The remaining memory size (MAX_MEMORY_SIZE - SEGMENT_0_SIZE) must be evenly divisible by this segment size
        // TODO: Could be configurable
        public const uint ADDITIONAL_SEGMENT_SIZE = 1024*8; // 8192 /  8KB  (0x2000-0x3fff, 0x4000-0x5fff, ..., 0e000-0xffff)

        /// <summary>
        /// Writing to this address will change the contents of the memory segment specified by the value,
        /// by loading in the memory contents for the Segment Bank Number specified in address MEM_SWITCH_SEGMENT_BANK_NUMBER_ADDRESS.
        /// Memory location MEM_SWITCH_SEGMENT_BANK_NUMBER_ADDRESS must thus be prepared before writing to this address.
        /// </summary>
        private const ushort MEM_SWITCH_SEGMENT_NUMBER_ADDRESS = 0x0001;
        /// <summary>
        /// Specifies which Segment Bank Number to be loaded in to the memory Segment specified by MEM_SWITCH_SEGMENT_NUMBER_ADDRESS.
        /// The actual loading in of the bank is not performed until writing to MEM_SWITCH_SEGMENT_NUMBER_ADDRESS (where the Segment number should be written to)
        /// </summary>        
        private const ushort MEM_SWITCH_SEGMENT_BANK_NUMBER_ADDRESS = 0x0002;


        private readonly List<MemorySegment> _memorySegments;
        public List<MemorySegment> MemorySegments => _memorySegments;

        public byte[] Data { get => TotalMemoryFromAllSegments();}

        public uint Size => TotalMemoryLengthFromAllSegments();

        private byte[] _optimizedMemory;

        private bool _bankSwitchingEnabled;
        
        public byte this[ushort index] 
        {
            get
            {
                return _optimizedMemory[index];
                // var bank = GetSegmentAndOffsetFromMemoryAddress(index, out ushort bankOffset);
                // return _memorySegments[bank][bankOffset];
            }
            set
            {
                // var bank = GetSegmentAndOffsetFromMemoryAddress(index, out ushort bankOffset);
                // _memorySegments[bank][bankOffset] = value;
                 _optimizedMemory[index] = value;

                // Check if we are writing to a special location that will trigger loading of memory bank in to a segment.
                if(_bankSwitchingEnabled && index == MEM_SWITCH_SEGMENT_NUMBER_ADDRESS)
                {
                    byte segmentNumber = value;
                    byte segmentBankNumber = this[MEM_SWITCH_SEGMENT_BANK_NUMBER_ADDRESS];
                    ChangeCurrentSegmentBank(segmentNumber, segmentBankNumber);
                }
            }
        }

        public Memory(bool enableBankSwitching = false): this(MAX_MEMORY_SIZE, enableBankSwitching)
        {
        }

        public Memory(uint memorySize, bool enableBankSwitching = false)
        {
            _bankSwitchingEnabled = enableBankSwitching;

            if(memorySize<SEGMENT_0_SIZE)
                throw new ArgumentException($"The specified memorySize {memorySize} is less than minimum allowed memory size {SEGMENT_0_SIZE}", nameof(memorySize));

            if(memorySize>MAX_MEMORY_SIZE)
                throw new ArgumentException($"The specified memorySize {memorySize} is greater than maximum allowed memory size {MAX_MEMORY_SIZE}", nameof(memorySize));

            if((memorySize - SEGMENT_0_SIZE) % ADDITIONAL_SEGMENT_SIZE !=0)
                throw new ArgumentException($"The size of the memory above the required minimum {SEGMENT_0_SIZE} must be evenly divisible by the bank size {ADDITIONAL_SEGMENT_SIZE}", nameof(memorySize));


            // Add required segment 0 with a minimum size, not changable.
            ushort segmentStartAddess = 0x0000;
            var memorySegments = new List<MemorySegment>
            {
                new MemorySegment(segmentStartAddess, SEGMENT_0_SIZE)
            };

            // Create additional segments until we reach required total memory size
            var noAdditionalBanks = (memorySize - SEGMENT_0_SIZE) / ADDITIONAL_SEGMENT_SIZE;
            for (byte i = 0; i < noAdditionalBanks; i++)
            {
                segmentStartAddess += (ushort) memorySegments[i].Size; // Adds previous segments size (i=0 already added above)
                memorySegments.Add(new MemorySegment(segmentStartAddess, ADDITIONAL_SEGMENT_SIZE));
            }
            _memorySegments = memorySegments;
            BuildOptimizedMemory();
        }

        public Memory(List<MemorySegment> memorySegments, bool enableBankSwitching)
        {
            _memorySegments = memorySegments;
            _bankSwitchingEnabled = enableBankSwitching;
            BuildOptimizedMemory();
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
            var additionalBankNumber = Math.DivRem((int)(address - SEGMENT_0_SIZE), (int)ADDITIONAL_SEGMENT_SIZE, out int remainder);
            bankOffset = (ushort)remainder;
            return (byte) (additionalBankNumber + 1) ;
        }

        public void ChangeCurrentSegmentBank(byte segmentNumber, byte segmentBankNumber)
        {         
            if(segmentNumber == 0)
                throw new ArgumentException($"Segment 0 can not be changed.", nameof(segmentNumber));
            if(segmentNumber >= _memorySegments.Count)
                throw new ArgumentException($"Maximum segmentNumber is {_memorySegments.Count-1}", nameof(segmentNumber));

            byte previousSegmentBankNumber = _memorySegments[segmentNumber].CurrentBankNumber;

            _memorySegments[segmentNumber].ChangeCurrentSegmentBank(segmentBankNumber);

            UpdateOptimizedMemory(segmentNumber, previousSegmentBankNumber);
        }

        /// <summary>
        /// Sets a new empty bank (all memory locations with value 0) for specified segmentNumber and segmentBankNumber.
        /// </summary>
        /// <param name="segmentNumber"></param>
        /// <param name="segmentBankNumber"></param>
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
            AssertBankSwitchingEnabled();

            if(segmentNumber == 0)
                throw new ArgumentException($"Segment 0 can not have multiple memory banks.", nameof(segmentNumber));
            if(segmentNumber >= _memorySegments.Count)
                throw new ArgumentException($"Maximum segmentNumber is {_memorySegments.Count-1}", nameof(segmentNumber));

            if(segmentBankContent.Length != ADDITIONAL_SEGMENT_SIZE)
                throw new ArgumentException($"The memory size for a segment bank must be {ADDITIONAL_SEGMENT_SIZE}.", nameof(segmentBankContent));

            _memorySegments[segmentNumber].AddSegmentBank(segmentBankContent);
        }

        /// <summary>
        /// Updates the specified segmentNumber and segmentBankNumber with a memory array.
        /// SegmentNumber 0 not allowed to use (it's the required first X bytes of memory)
        /// SegmentBankNumber 0 not allowed to use (it's the original memory)
        /// </summary>
        /// <param name="segmentNumber">The segment number that should be configured.</param>
        /// <param name="segmentBankNumber">The segment bank id within the segment (unique per segment).</param>
        /// <param name="segmentBankContent">The memory byte array for the segmentNumber and segmentBankNumber.</param>
        public void UpdateMemorySegmentBank(byte segmentNumber, byte segmentBankNumber, byte[] segmentBankContent)
        {
            AssertBankSwitchingEnabled();

            if(segmentNumber == 0)
                throw new ArgumentException($"Segment 0 can not be changed.", nameof(segmentNumber));
            if(segmentNumber >= _memorySegments.Count)
                throw new ArgumentException($"Maximum segmentNumber is {_memorySegments.Count-1}", nameof(segmentNumber));

            if(segmentBankContent.Length != ADDITIONAL_SEGMENT_SIZE)
                throw new ArgumentException($"The memory size for a segment bank must be {ADDITIONAL_SEGMENT_SIZE}.", nameof(segmentBankContent));

            byte previousSegmentBankNumber = _memorySegments[segmentNumber].CurrentBankNumber;

            _memorySegments[segmentNumber].UpdateSegmentBank(segmentBankNumber, segmentBankContent);

            UpdateOptimizedMemory(segmentNumber, previousSegmentBankNumber);
        }

        private void AssertBankSwitchingEnabled()
        {
            if(!_bankSwitchingEnabled)
                throw new DotNet6502Exception($"Bank switching has not been enabled. Enable this option when {nameof(Memory)} is created.");
        }

        private void BuildOptimizedMemory()
        {
            _optimizedMemory = TotalMemoryFromAllSegments();
        }

        private void UpdateOptimizedMemory(byte segmentNumber, byte previousSegmentBankNumber)
        {
            ushort segmentStartAddress = MemorySegments[segmentNumber].StartAddress;

            // Copy current optimized memory to the previous SegmentBankNumber
            Buffer.BlockCopy(_optimizedMemory, segmentStartAddress, MemorySegments[segmentNumber].Banks[previousSegmentBankNumber].Memory, 0, (int) MemorySegments[segmentNumber].Size);

            // Copy the new (current) SegmentBankNumber to optimized memory
            Buffer.BlockCopy(MemorySegments[segmentNumber].Memory, 0, _optimizedMemory, segmentStartAddress, (int) MemorySegments[segmentNumber].Size);
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
            var memoryClone = new Memory(_memorySegments, _bankSwitchingEnabled);
            return memoryClone;
        }
    }
}
