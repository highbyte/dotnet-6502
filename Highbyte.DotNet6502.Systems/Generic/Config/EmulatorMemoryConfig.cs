using System.Drawing;
using Highbyte.DotNet6502.Systems.Generic.Video;

namespace Highbyte.DotNet6502.Systems.Generic.Config;

public class EmulatorMemoryConfig
{
    private bool _isDirty = false;
    private EmulatorScreenConfig _screen;
    private EmulatorInputConfig _input;

    public bool IsDirty => _isDirty;
    public void ClearDirty()
    {
        _isDirty = false;
    }

    public EmulatorScreenConfig Screen
    {
        get { return _screen; }
        set
        {
            _screen = value;
            _isDirty = true;
        }
    }
    public EmulatorInputConfig Input
    {
        get { return _input; }
        set
        {
            _input = value;
            _isDirty = true;
        }
    }

    public EmulatorMemoryConfig()
    {
        Screen = new();
        Input = new();
    }

    public EmulatorMemoryConfig Clone()
    {
        return new EmulatorMemoryConfig
        {
            Screen = Screen.Clone(),
            Input = Input.Clone()
        };
    }

    public bool Validate(out List<string> validationErrors)
    {
        validationErrors = new List<string>();

        ushort screenMemoryStart = Screen.ScreenStartAddress;
        ushort screenMemoryEnd = (ushort)(screenMemoryStart + (ushort)(Screen.Cols * Screen.Rows));

        ushort colorMemoryStart = Screen.ScreenColorStartAddress;
        ushort colorMemoryEnd = (ushort)(colorMemoryStart + (ushort)(Screen.Cols * Screen.Rows));

        // --------------------------------
        // Screen & color addresses
        // --------------------------------
        // Validate so screen memory and color memory don't overlap
        if (Overlap(screenMemoryStart, screenMemoryEnd, colorMemoryStart, colorMemoryEnd))
            validationErrors.Add("Screen and Color memory address space overlaps");

        // Validate so Background color address falls within screen or color addresses
        if (Within(Screen.ScreenBackgroundColorAddress, screenMemoryStart, screenMemoryEnd))
            validationErrors.Add("ScreenBackgroundColorAddress cannot be in screen memory.");
        if (Within(Screen.ScreenBackgroundColorAddress, colorMemoryStart, colorMemoryEnd))
            validationErrors.Add("ScreenBackgroundColorAddress cannot be in screen memory.");

        // Validate so Border color address falls within screen or color addresses
        if (Within(Screen.ScreenBorderColorAddress, screenMemoryStart, screenMemoryEnd))
            validationErrors.Add("ScreenBorderColorAddress cannot be in screen memory.");
        if (Within(Screen.ScreenBorderColorAddress, colorMemoryStart, colorMemoryEnd))
            validationErrors.Add("ScreenBorderColorAddress cannot be in screen memory.");

        // --------------------------------
        // Input addresses
        // --------------------------------
        // Validate so Keyboard input address falls within screen or color addresses
        if (Within(Input.KeyPressedAddress, screenMemoryStart, screenMemoryEnd))
            validationErrors.Add("KeyPressedAddress cannot be in screen memory.");
        if (Within(Input.KeyPressedAddress, colorMemoryStart, colorMemoryEnd))
            validationErrors.Add("KeyPressedAddress cannot be in color memory.");
        if (Within(Input.KeyDownAddress, screenMemoryStart, screenMemoryEnd))
            validationErrors.Add("KeyDownAddress cannot be in screen memory.");
        if (Within(Input.KeyDownAddress, colorMemoryStart, colorMemoryEnd))
            validationErrors.Add("KeyDownAddress cannot be in color memory.");
        if (Within(Input.KeyReleasedAddress, screenMemoryStart, screenMemoryEnd))
            validationErrors.Add("KeyReleasedAddress cannot be in color memory.");
        if (Within(Input.KeyReleasedAddress, colorMemoryStart, colorMemoryEnd))
            validationErrors.Add("KeyReleasedAddress cannot be in color memory.");


        // --------------------------------
        // Character and color maps
        // --------------------------------
        // if(!Screen.UseAscIICharacters && Screen.CharacterMap==null)
        //     validationErrors.Add($"If {nameof(Screen.UseAscIICharacters)} is false, {nameof(Screen.CharacterMap)} must be set to a character map.");

        return validationErrors.Count == 0;
    }

    private bool Within(ushort address, ushort startAddress, ushort endAddress)
    {
        return address >= startAddress && address <= endAddress;
    }

    private bool Overlap(ushort address1Start, ushort address1End, ushort address2Start, ushort address2End)
    {
        return !(address1End < address2Start || address1Start > address2End);
    }
}

public class EmulatorScreenConfig
{
    private bool _isDirty = false;
    private int _cols;
    private int _rows;
    private int _borderCols;
    private int _borderRows;
    private ushort _screenStartAddress;
    private ushort _screenColorStartAddress;
    private ushort _screenRefreshStatusAddress;
    private ushort _screenBorderColorAddress;
    private byte _defaultFgColor;
    private byte _defaultBgColor;
    private byte _defaultBorderColor;
    private Dictionary<byte, Color> _colorMap;
    private bool _useAscIICharacters;
    private Dictionary<string, byte> _characterMap;

    public bool IsDirty => _isDirty;
    public void ClearDirty()
    {
        _isDirty = false;
    }


    public int Cols
    {
        get { return _cols; }
        set
        {
            _cols = value;
            _isDirty = true;
        }
    }
    public int Rows
    {
        get { return _rows; }
        set
        {
            _rows = value;
            _isDirty = true;
        }
    }
    public int BorderCols
    {
        get { return _borderCols; }
        set
        {
            _borderCols = value;
            _isDirty = true;
        }
    }
    public int BorderRows
    {
        get { return _borderRows; }
        set
        {
            _borderRows = value;
            _isDirty = true;
        }
    }

    public ushort ScreenStartAddress
    {
        get { return _screenStartAddress; }
        set
        {
            _screenStartAddress = value;
            _isDirty = true;
        }
    }
    public ushort ScreenColorStartAddress
    {
        get { return _screenColorStartAddress; }
        set
        {
            _screenColorStartAddress = value;
            _isDirty = true;
        }
    }

    public ushort ScreenRefreshStatusAddress
    {
        get { return _screenRefreshStatusAddress; }
        set
        {
            _screenRefreshStatusAddress = value;
            _isDirty = true;
        }
    }

    public ushort ScreenBorderColorAddress
    {
        get { return _screenBorderColorAddress; }
        set
        {
            _screenBorderColorAddress = value;
            _isDirty = true;
        }
    }
    public ushort ScreenBackgroundColorAddress { get; set; }

    public byte DefaultFgColor
    {
        get { return _defaultFgColor; }
        set
        {
            _defaultFgColor = value;
            _isDirty = true;
        }
    }
    public byte DefaultBgColor
    {
        get { return _defaultBgColor; }
        set
        {
            _defaultBgColor = value;
            _isDirty = true;
        }
    }
    public byte DefaultBorderColor
    {
        get { return _defaultBorderColor; }
        set
        {
            _defaultBorderColor = value;
            _isDirty = true;
        }
    }

    public Dictionary<byte, Color> ColorMap
    {
        get { return _colorMap; }
        set
        {
            _colorMap = value;
            _isDirty = true;
        }
    }
    public bool UseAscIICharacters
    {
        get { return _useAscIICharacters; }
        set
        {
            _useAscIICharacters = value;
            _isDirty = true;
        }
    }

    /// <summary>
    /// If UseAscIICharacters is false, set a custom character map in CharacterMap
    /// </summary>
    /// <value></value>
    //public Dictionary<byte,byte> CharacterMap { get; set; }
    public Dictionary<string, byte> CharacterMap
    {
        get { return _characterMap; }
        set
        {
            _characterMap = value;
            _isDirty = true;
        }
    }

    public EmulatorScreenConfig()
    {
        Cols = 80;
        Rows = 25;
        BorderCols = 0;
        BorderRows = 0;

        // Mimic C64 for some memory addresses (though we have 80 cols here instead of 40)
        ScreenStartAddress = 0x0400;   //80*25 = 2000(0x07d0) -> range 0x0400 - 0x0bcf
        ScreenColorStartAddress = 0xd800;   //80*25 = 2000(0x07d0) -> range 0xd800 - 0xdfcf

        ScreenRefreshStatusAddress = 0xd000;   // To sync 6502 code with host frame: The 6502 code should wait for bit 0 to become set, and then wait for it to become cleared.

        ScreenBorderColorAddress = 0xd020;
        ScreenBackgroundColorAddress = 0xd021;

        DefaultFgColor = 0x0e;  // 0x0e = Light blue
        DefaultBgColor = 0x06;  // 0x06 = Blue
        DefaultBorderColor = 0x0e;  // 0x0e = Blue

        ColorMap = ColorMaps.GenericColorMap;        // Default to C64 color map. TODO: Make it configurable from config file (via enum?)

        UseAscIICharacters = true;

        //CharacterMap = CharacterMaps.PETSCIIMap; // TODO
    }

    public EmulatorScreenConfig Clone()
    {
        return new EmulatorScreenConfig
        {
            Cols = Cols,
            Rows = Rows,
            BorderCols = BorderCols,
            BorderRows = BorderRows,

            ScreenStartAddress = ScreenStartAddress,
            ScreenColorStartAddress = ScreenColorStartAddress,
            ScreenRefreshStatusAddress = ScreenRefreshStatusAddress,
            ScreenBorderColorAddress = ScreenBorderColorAddress,
            ScreenBackgroundColorAddress = ScreenBackgroundColorAddress,

            DefaultFgColor = DefaultFgColor,
            DefaultBorderColor = DefaultBorderColor,
            ColorMap = ColorMap,

            UseAscIICharacters = UseAscIICharacters,
            CharacterMap = CharacterMap,
        };
    }
}

public class EmulatorInputConfig
{
    private bool _isDirty = false;
    private ushort _keyPressedAddress;
    private ushort _keyDownAddress;
    private ushort _keyReleasedAddress;
    private ushort _randomValueAddress;

    public bool IsDirty => _isDirty;
    public void ClearDirty()
    {
        _isDirty = false;
    }

    // Keyboard
    public ushort KeyPressedAddress
    {
        get { return _keyPressedAddress; }
        set
        {
            _keyPressedAddress = value;
            _isDirty = true;
        }
    }
    public ushort KeyDownAddress
    {
        get { return _keyDownAddress; }
        set
        {
            _keyDownAddress = value;
            _isDirty = true;
        }
    }
    public ushort KeyReleasedAddress
    {
        get { return _keyReleasedAddress; }
        set
        {
            _keyReleasedAddress = value;
            _isDirty = true;
        }
    }
    public ushort RandomValueAddress
    {
        get { return _randomValueAddress; }
        set
        {
            _randomValueAddress = value;
            _isDirty = true;
        }
    }

    public EmulatorInputConfig()
    {
        KeyPressedAddress = 0xe000;
        KeyDownAddress = 0xe001;
        KeyReleasedAddress = 0xe002;
        RandomValueAddress = 0xd41b;
    }

    public EmulatorInputConfig Clone()
    {
        return new EmulatorInputConfig
        {
            KeyPressedAddress = KeyPressedAddress,
            KeyDownAddress = KeyDownAddress,
            KeyReleasedAddress = KeyReleasedAddress,
            RandomValueAddress = RandomValueAddress,
        };
    }
}
