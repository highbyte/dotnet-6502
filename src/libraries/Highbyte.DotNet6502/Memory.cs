namespace Highbyte.DotNet6502;

public class Memory
{
    public delegate byte LoadByte(ushort address);
    public delegate void StoreByte(ushort address, byte value);

    public delegate bool PreWriteIntercept(ushort address, byte value);
    public class MemValue
    {
        public byte Value { get; set; }
    }

    public const int MAX_MEMORY_SIZE = 1024 * 64;

    public int Size { get; private set; }
    public int NumberOfConfigurations { get; private set; }
    public int CurrentConfiguration { get; private set; }

    private LoadByte[] _readers = null!;
    private StoreByte[] _writers = null!;
    private StoreByte[] _originalWriters = null!;

    private readonly LoadByte[][] _readersPerConfiguration;
    private readonly StoreByte[][] _writersPerConfiguration;
    private readonly StoreByte[][] _originalWritersPerConfiguration;

    public Memory(int memorySize = MAX_MEMORY_SIZE, int numberOfConfigurations = 1, bool mapToDefaultRAM = true)
    {
        if (memorySize <= 0)
            throw new ArgumentException("Must be greater than 0", nameof(memorySize));
        if (memorySize > MAX_MEMORY_SIZE)
            throw new ArgumentException($"Must be less than or equal to {MAX_MEMORY_SIZE}", nameof(memorySize));
        if (numberOfConfigurations <= 0)
            throw new ArgumentException("Must be equal to or greater than 1", nameof(numberOfConfigurations));

        Size = memorySize;
        NumberOfConfigurations = numberOfConfigurations;
        _readersPerConfiguration = new LoadByte[numberOfConfigurations][];
        _writersPerConfiguration = new StoreByte[numberOfConfigurations][];
        _originalWritersPerConfiguration = new StoreByte[numberOfConfigurations][];

        for (int i = 0; i < numberOfConfigurations; i++)
        {
            _readersPerConfiguration[i] = new LoadByte[memorySize];
            _writersPerConfiguration[i] = new StoreByte[memorySize];
            _originalWritersPerConfiguration[i] = new StoreByte[memorySize];
            if (mapToDefaultRAM)
            {
                SetMemoryConfiguration(i);
                MapRAM(0x0000, new byte[Size]);
            }
        }
        SetMemoryConfiguration(0);
    }

    public void SetMemoryConfiguration(int configuration)
    {
        if (configuration >= NumberOfConfigurations)
            throw new ArgumentException($"Memory configuration doesn't exist. Max value is {NumberOfConfigurations - 1} ", nameof(configuration));
        CurrentConfiguration = configuration;
        _readers = _readersPerConfiguration[CurrentConfiguration];
        _writers = _writersPerConfiguration[CurrentConfiguration];
        _originalWriters = _originalWritersPerConfiguration[CurrentConfiguration];
    }

    public byte this[ushort index]
    {
        get
        {
            return Read(index);
        }
        set
        {
            Write(index, value);
        }
    }

    public void MapRAM(ushort baseAddress, byte[] data, ushort dataOffset = 0, ushort? length = null, PreWriteIntercept? preWriteIntercept = null)
    {
        LoadByte reader = delegate (ushort address)
        {
            return data[(address + dataOffset) - baseAddress];
        };
        StoreByte writer;
        StoreByte originalWriter;
        if (preWriteIntercept == null)
        {
            writer = delegate (ushort address, byte value)
            {
                data[(address + dataOffset) - baseAddress] = value;
            };
            originalWriter = delegate (ushort address, byte value)
            {
                data[(address + dataOffset) - baseAddress] = value;
            };
        }
        else
        {
            writer = delegate (ushort address, byte value)
            {
                var actualAddress = (ushort)((address + dataOffset) - baseAddress);
                var shouldWrite = preWriteIntercept(actualAddress, value);
                if (shouldWrite)
                    data[actualAddress] = value;
            };
            originalWriter = delegate (ushort address, byte value)
            {
                var actualAddress = (ushort)((address + dataOffset) - baseAddress);
                var shouldWrite = preWriteIntercept(actualAddress, value);
                if (shouldWrite)
                    data[actualAddress] = value;
            };

        }

        // Func<ushort, byte> reader = (ushort address) =>
        // {
        //     return data[baseAddress - address];
        // };
        // Action<ushort, byte> writer = (ushort address, byte value) =>
        // {
        //     data[baseAddress - address] = value;
        // };

        int dataLength = length ?? data.Length;
        for (int i = 0; i < dataLength; i++)
        {
            _readers[baseAddress + i] = reader;
            _writers[baseAddress + i] = writer;
            _originalWriters[baseAddress + i] = originalWriter;
        }
    }

    public void MapROM(ushort baseAddress, byte[] data)
    {
        LoadByte reader = delegate (ushort address)
        {
            return data[address - baseAddress];
        };
        // Func<ushort, byte> reader = (ushort address) =>
        // {
        //     return data[address - baseAddress];
        // };
        for (int i = 0; i < data.Length; i++)
        {
            _readers[baseAddress + i] = reader;
            // TODO: Should we check that there is a writer registered (RAM) for the same location? If not, calling Write could generate exception.
        }
    }

    public void MapRO(ushort address, MemValue memValue)
    {
        LoadByte reader = _ => { return memValue.Value; };
        _readers[address] = reader;
    }

    public void MapWO(ushort address, MemValue memValue)
    {
        StoreByte writer = (_, newVal) => { memValue.Value = newVal; };
        _writers[address] = writer;
    }

    public void MapRW(ushort address, MemValue memValue)
    {
        MapRO(address, memValue);
        MapWO(address, memValue);
    }

    /// <summary>
    /// Maps an individual address to a delegate for reading 
    /// </summary>
    /// <param name="address"></param>
    /// <param name="reader"></param>
    public void MapReader(ushort address, LoadByte reader)
    {
        _readers[address] = reader;
    }

    /// <summary>
    /// Maps an individual address to a an delegate for writing
    /// </summary>
    /// <param name="address"></param>
    /// <param name="writer"></param>
    public void MapWriter(ushort address, StoreByte writer)
    {
        _writers[address] = writer;
    }

    /// <summary>
    /// Returns the value at a memory location. 
    /// If ROM has been mapped after RAM on the same location, the ROM value will be returned.
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public byte Read(ushort address)
    {
        return _readers[address](address);
    }

    /// <summary>
    /// Writes a value to a memory location.
    /// If ROM has been mapped after RAM on the same location, the RAM value will be updated.
    /// </summary>
    /// <param name="address"></param>
    /// <param name="value"></param>
    public void Write(ushort address, byte value)
    {
        _writers[address](address, value);
    }

    /// <summary>
    /// Returns value at a memory location as originally configured, even if another map (RAM or ROM) has been created to the same location afterwards.
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    ////public byte ReadOriginal(ushort address)
    ////{
    ////    return _originalReaders[address](address);
    ////}

    /// <summary>
    /// Writes a value to a memory location as originally configured, even if another map (RAM or ROM) has been created to the same location afterwards.
    /// </summary>
    /// <param name="address"></param>
    /// <param name="value"></param>
    public void WriteOriginal(ushort address, byte value)
    {
        _originalWriters[address](address, value);
    }

    // TODO: Implement Clone() method correctly if this method is needed. Currently it won't clone the delegates correctly.
    //public Memory Clone()
    //{
    //    var memoryClone = new Memory
    //    {
    //        _readers = (LoadByte[])this._readers.Clone(),
    //        _writers = (StoreByte[])this._writers.Clone(),
    //        _readersPerConfiguration = (LoadByte[][])this._readersPerConfiguration.Clone(),
    //        _writersPerConfiguration = (StoreByte[][])this._writersPerConfiguration.Clone(),
    //        _originalWritersPerConfiguration = (StoreByte[][])this._originalWritersPerConfiguration.Clone(),
    //        CurrentConfiguration = this.CurrentConfiguration,
    //        NumberOfConfigurations = this.NumberOfConfigurations,
    //    };
    //    return memoryClone;
    //}
}
