namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// A single VIC-II sprite.
/// </summary>
public class Vic2Sprite
{
    public const int DEFAULT_WIDTH = 24;
    public const int DEFAULT_HEIGTH = 21;

    private readonly Vic2SpriteManager _spriteManager;
    private Vic2 _vic2 => _spriteManager.Vic2;
    private C64 _c64 => _spriteManager.Vic2.C64;

    public int SpriteNumber { get; private set; }
    public bool Visible => _c64.ReadIOStorage(Vic2Addr.SPRITE_ENABLE).IsBitSet(SpriteNumber);
    public int X => _c64.ReadIOStorage((ushort)(Vic2Addr.SPRITE_0_X + SpriteNumber * 2)) +
                    (_c64.ReadIOStorage(Vic2Addr.SPRITE_MSB_X).IsBitSet(SpriteNumber) ? 256 : 0);
    public int Y => _c64.ReadIOStorage((ushort)(Vic2Addr.SPRITE_0_Y + SpriteNumber * 2));
    public byte Color => _c64.ReadIOStorage((ushort)(Vic2Addr.SPRITE_0_COLOR + SpriteNumber));
    public bool Multicolor => _c64.ReadIOStorage(Vic2Addr.SPRITE_MULTICOLOR_ENABLE).IsBitSet(SpriteNumber);
    public bool DoubleWidth => _c64.ReadIOStorage(Vic2Addr.SPRITE_X_EXPAND).IsBitSet(SpriteNumber);
    public bool DoubleHeight => _c64.ReadIOStorage(Vic2Addr.SPRITE_Y_EXPAND).IsBitSet(SpriteNumber);
    public bool PriorityOverForeground => !_c64.ReadIOStorage(Vic2Addr.SPRITE_FOREGROUND_PRIO).IsBitSet(SpriteNumber);

    public int WidthPixels => DoubleWidth ? DEFAULT_WIDTH * 2 : DEFAULT_WIDTH;
    public int WidthBytes => WidthPixels / 8;
    public int HeightPixels => DoubleHeight ? DEFAULT_HEIGTH * 2 : DEFAULT_HEIGTH;

    private Vic2SpriteData _data = new Vic2SpriteData();
    public Vic2SpriteData Data => BuildSpriteData();

    public bool IsDirty => _isDirty;

    private bool _isDirty = true;

    public Vic2Sprite(int spriteNumber, Vic2SpriteManager spriteManager)
    {
        SpriteNumber = spriteNumber;
        _spriteManager = spriteManager;
    }

    private Vic2SpriteData BuildSpriteData()
    {
        var spritePointer = _vic2.Vic2Mem[(ushort)(Vic2SpriteManager.SPRITE_POINTERS_START_ADDRESS + SpriteNumber)];
        var spritePointerAddress = (ushort)(spritePointer * 64);

        var bytesPerRow = DEFAULT_WIDTH / 8;
        for (int row = 0; row < DEFAULT_HEIGTH; row++)
        {
            for (int rowByte = 0; rowByte < bytesPerRow; rowByte++)
            {
                var byteAddr = spritePointerAddress + (row * bytesPerRow) + rowByte;
                var spriteRowByte = _vic2.Vic2Mem[(ushort)(byteAddr)];
                _data.Rows[row].Bytes[rowByte] = spriteRowByte;
            }
        }

        return _data;
    }

    public void HasChanged(Vic2SpriteChangeType spriteChangeType)
    {
        switch (spriteChangeType)
        {
            case Vic2SpriteChangeType.Data:
                SetDirty();
                break;
            case Vic2SpriteChangeType.Color:
                if (Multicolor)
                    SetDirty();
                break;
            case Vic2SpriteChangeType.MultiColor0:
                if (Multicolor)
                    SetDirty();
                break;
            case Vic2SpriteChangeType.MultiColor1:
                if (Multicolor)
                    SetDirty();
                break;
            case Vic2SpriteChangeType.All:
                SetDirty();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(spriteChangeType), spriteChangeType, null);
        }
    }

    private void SetDirty()
    {
        _isDirty = true;
    }

    public void ClearDirty()
    {
        _isDirty = false;
    }

    private void CreateTestSpriteImage()
    {
        // Fake sprite data
        _data.Rows[00].Bytes = new byte[] { 0b11111111, 0b11111111, 0b11111111 };
        _data.Rows[01].Bytes = new byte[] { 0b10000000, 0b00111100, 0b00000001 };
        _data.Rows[02].Bytes = new byte[] { 0b10000000, 0b00111100, 0b00000001 };
        _data.Rows[03].Bytes = new byte[] { 0b10000000, 0b00111100, 0b00000001 };
        _data.Rows[04].Bytes = new byte[] { 0b10000000, 0b00111100, 0b00000001 };
        _data.Rows[05].Bytes = new byte[] { 0b10000000, 0b00111100, 0b00000001 };
        _data.Rows[06].Bytes = new byte[] { 0b10000000, 0b00111100, 0b00000001 };
        _data.Rows[07].Bytes = new byte[] { 0b10000000, 0b00111100, 0b00000001 };
        _data.Rows[08].Bytes = new byte[] { 0b11111111, 0b11111111, 0b11111111 };
        _data.Rows[09].Bytes = new byte[] { 0b11111111, 0b11111111, 0b11111111 };

        _data.Rows[10].Bytes = new byte[] { 0b11111111, 0b11111111, 0b11111111 };
        _data.Rows[11].Bytes = new byte[] { 0b11111111, 0b11111111, 0b11111111 };
        _data.Rows[12].Bytes = new byte[] { 0b10000000, 0b00111100, 0b00000001 };
        _data.Rows[13].Bytes = new byte[] { 0b10000000, 0b00111100, 0b00000001 };
        _data.Rows[14].Bytes = new byte[] { 0b10000000, 0b00111100, 0b00000001 };
        _data.Rows[15].Bytes = new byte[] { 0b10000000, 0b00111100, 0b00000001 };
        _data.Rows[16].Bytes = new byte[] { 0b10000000, 0b00111100, 0b00000001 };
        _data.Rows[17].Bytes = new byte[] { 0b10000000, 0b00111100, 0b00000001 };
        _data.Rows[18].Bytes = new byte[] { 0b10000000, 0b00111100, 0b00000001 };
        _data.Rows[19].Bytes = new byte[] { 0b10000000, 0b00111100, 0b00000001 };

        _data.Rows[20].Bytes = new byte[] { 0b11111111, 0b11111111, 0b11111111 };
    }

    private void CreateTestMultiColorSpriteImage()
    {
        // Fake multi-color sprite data
        // Each multi-color pixel is 2 pixels wide.
        // 00 = Background color (transparent)
        // 01 = Sprite multicolor register 0 (53285, $D025) shared by all sprites
        // 10 = Sprite Color Registers (53287-94, $D027-E), color per sprite
        // 11 = Sprite multicolor register 1 (53286, $D026) shared by all sprites
        _data.Rows[00].Bytes = new byte[] { 0b01_01_01_01, 0b01_01_01_01, 0b01_01_01_01 };
        _data.Rows[01].Bytes = new byte[] { 0b01_00_00_00, 0b00_10_10_00, 0b00_00_00_01 };
        _data.Rows[01].Bytes = new byte[] { 0b01_00_00_00, 0b00_10_10_00, 0b00_00_00_01 };
        _data.Rows[01].Bytes = new byte[] { 0b01_00_00_00, 0b00_10_10_00, 0b00_00_00_01 };
        _data.Rows[01].Bytes = new byte[] { 0b01_00_00_00, 0b00_10_10_00, 0b00_00_00_01 };
        _data.Rows[01].Bytes = new byte[] { 0b01_00_00_00, 0b00_10_10_00, 0b00_00_00_01 };
        _data.Rows[01].Bytes = new byte[] { 0b01_00_00_00, 0b00_10_10_00, 0b00_00_00_01 };
        _data.Rows[01].Bytes = new byte[] { 0b01_00_00_00, 0b00_10_10_00, 0b00_00_00_01 };
        _data.Rows[00].Bytes = new byte[] { 0b01_01_01_01, 0b01_01_01_01, 0b01_01_01_01 };
        _data.Rows[00].Bytes = new byte[] { 0b01_01_01_01, 0b01_01_01_01, 0b01_01_01_01 };

        _data.Rows[00].Bytes = new byte[] { 0b01_01_01_01, 0b01_01_01_01, 0b01_01_01_01 };
        _data.Rows[00].Bytes = new byte[] { 0b01_01_01_01, 0b01_01_01_01, 0b01_01_01_01 };
        _data.Rows[01].Bytes = new byte[] { 0b11_00_00_00, 0b00_11_11_00, 0b00_00_00_11 };
        _data.Rows[01].Bytes = new byte[] { 0b11_00_00_00, 0b00_11_11_00, 0b00_00_00_11 };
        _data.Rows[01].Bytes = new byte[] { 0b11_00_00_00, 0b00_11_11_00, 0b00_00_00_11 };
        _data.Rows[01].Bytes = new byte[] { 0b11_00_00_00, 0b00_11_11_00, 0b00_00_00_11 };
        _data.Rows[01].Bytes = new byte[] { 0b11_00_00_00, 0b00_11_11_00, 0b00_00_00_11 };
        _data.Rows[01].Bytes = new byte[] { 0b11_00_00_00, 0b00_11_11_00, 0b00_00_00_11 };
        _data.Rows[01].Bytes = new byte[] { 0b11_00_00_00, 0b00_11_11_00, 0b00_00_00_11 };
        _data.Rows[01].Bytes = new byte[] { 0b11_00_00_00, 0b00_11_11_00, 0b00_00_00_11 };

        _data.Rows[20].Bytes = new byte[] { 0b11_11_11_11, 0b11_11_11_11, 0b11_11_11_11 };
    }

    public class Vic2SpriteData
    {
        public Vic2SpriteRow[] Rows { get; set; } = new Vic2SpriteRow[DEFAULT_HEIGTH];

        public Vic2SpriteData()
        {
            for (int row = 0; row < DEFAULT_HEIGTH; row++)
            {
                Rows[row] = new Vic2SpriteRow();
            }
        }

        public class Vic2SpriteRow
        {
            public byte[] Bytes { get; set; } = new byte[DEFAULT_WIDTH / 8];
        }
    }

    public enum Vic2SpriteChangeType
    {
        Color,
        MultiColor0,
        MultiColor1,
        Data,
        All,
    }
}
