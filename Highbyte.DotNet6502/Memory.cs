using System;

namespace Highbyte.DotNet6502
{
    public class Memory
    {
        // TODO: MIN_MEMORY_SIZE should not be 0, but maybe smaller than currently allowed?
        const uint MIN_MEMORY_SIZE = 1*64;    // 0x1000
        const uint MAX_MEMORY_SIZE = 1024*64; // 0xffff
        private readonly byte[] _data;
        public byte[] Data { get => _data;}

        public uint Size => (uint) _data.Length;
        
        public byte this[ushort index] 
        {
            get
            {
                return _data[index];
            }
            set
            {
                _data[index] = value;
            }
        }

        public Memory(): this(MAX_MEMORY_SIZE)
        {
        }

        
        public Memory(uint memorySize)
        {
            if(memorySize<MIN_MEMORY_SIZE)
                throw new ArgumentException($"The specified memorySize {memorySize} is less than minimum allowed memory size {MIN_MEMORY_SIZE}", nameof(memorySize));

            if(memorySize>MAX_MEMORY_SIZE)
                throw new ArgumentException($"The specified memorySize {memorySize} is greater than maximum allowed memory size {MAX_MEMORY_SIZE}", nameof(memorySize));
            _data = new byte[memorySize];
        }

        public void StoreData(ushort address, byte[] data)
        {
            if((address + data.Length) > MAX_MEMORY_SIZE)
                throw new DotNet6502Exception($"Address {address} + size of data {data.Length} exceeds maximum memory limit {MAX_MEMORY_SIZE}");

            for (int i = 0; i < data.Length; i++)
            {
                _data[address+i] = data[i];  
            }
        }

        public byte[] ReadData(ushort address, ushort length)
        {
            if((address + length) > MAX_MEMORY_SIZE)
                throw new DotNet6502Exception($"Address {address} + length {length} exceeds maximum memory limit {MAX_MEMORY_SIZE}");

            byte[] readArray = new byte[length];
            for (int i = 0; i < length; i++)
            {
                readArray[i] = _data[address+i];
            }
            return readArray;
        }        

        public Memory Clone()
        {
            var memoryClone = new Memory(this.Size);
            memoryClone.StoreData(0x0000, Data);
            return memoryClone;
        }
    }
}
