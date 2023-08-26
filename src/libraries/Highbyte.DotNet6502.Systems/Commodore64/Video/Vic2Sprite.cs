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
    private C64 _c64 => _vic2.C64;
    private Memory _c64Mem => _c64.Mem;

    public int SpriteNumber { get; private set; }
    public bool Visible => _c64Mem[Vic2Addr.SPRITE_ENABLE].IsBitSet(SpriteNumber);
    public int X => _c64Mem[(ushort)(Vic2Addr.SPRITE_0_X + SpriteNumber * 2)] +
                    (_c64Mem[Vic2Addr.SPRITE_MSB_X].IsBitSet(SpriteNumber) ? 256 : 0);
    public int Y => _c64Mem[(ushort)(Vic2Addr.SPRITE_0_Y + SpriteNumber * 2)];
    public byte Color => _c64Mem[(ushort)(Vic2Addr.SPRITE_0_COLOR + SpriteNumber)];
    public bool Multicolor => false;
    public bool DoubleWidth => false;
    public bool DoubleHeight => false;
    public bool PriorityOverForeground => true;

    private Vic2SpriteData _data;
    public Vic2SpriteData Data => BuildSpriteData();

    public Vic2Sprite(int spriteNumber, Vic2SpriteManager spriteManager)
    {
        SpriteNumber = spriteNumber;
        _spriteManager = spriteManager;
    }

    private Vic2SpriteData BuildSpriteData()
    {
        if (_data == null)
            _data = new Vic2SpriteData();

        // TEST: Fake sprite data
        _data.Rows[00].Bytes = new byte[] { 0b11111111, 0b11111111, 0b11111111 };
        _data.Rows[01].Bytes = new byte[] { 0b00000000, 0b00111100, 0b00000000 };
        _data.Rows[02].Bytes = new byte[] { 0b00000000, 0b00111100, 0b00000000 };
        _data.Rows[03].Bytes = new byte[] { 0b00000000, 0b00111100, 0b00000000 };
        _data.Rows[04].Bytes = new byte[] { 0b00000000, 0b00111100, 0b00000000 };
        _data.Rows[05].Bytes = new byte[] { 0b00000000, 0b00111100, 0b00000000 };
        _data.Rows[06].Bytes = new byte[] { 0b00000000, 0b00111100, 0b00000000 };
        _data.Rows[07].Bytes = new byte[] { 0b00000000, 0b00111100, 0b00000000 };
        _data.Rows[08].Bytes = new byte[] { 0b11111111, 0b11111111, 0b11111111 };
        _data.Rows[09].Bytes = new byte[] { 0b11111111, 0b11111111, 0b11111111 };

        _data.Rows[10].Bytes = new byte[] { 0b11111111, 0b11111111, 0b11111111 };
        _data.Rows[11].Bytes = new byte[] { 0b11111111, 0b11111111, 0b11111111 };
        _data.Rows[12].Bytes = new byte[] { 0b00000000, 0b00111100, 0b00000000 };
        _data.Rows[13].Bytes = new byte[] { 0b00000000, 0b00111100, 0b00000000 };
        _data.Rows[14].Bytes = new byte[] { 0b00000000, 0b00111100, 0b00000000 };
        _data.Rows[15].Bytes = new byte[] { 0b00000000, 0b00111100, 0b00000000 };
        _data.Rows[16].Bytes = new byte[] { 0b00000000, 0b00111100, 0b00000000 };
        _data.Rows[17].Bytes = new byte[] { 0b00000000, 0b00111100, 0b00000000 };
        _data.Rows[18].Bytes = new byte[] { 0b00000000, 0b00111100, 0b00000000 };
        _data.Rows[19].Bytes = new byte[] { 0b00000000, 0b00111100, 0b00000000 };

        _data.Rows[20].Bytes = new byte[] { 0b11111111, 0b11111111, 0b11111111 };

        return _data;
    }

    public class Vic2SpriteData
    {
        public Vic2SpriteRow[] Rows { get; set; } = new Vic2SpriteRow[DEFAULT_HEIGTH];

        public Vic2SpriteData()
        {
            for (int row = 0; row < Vic2Sprite.DEFAULT_HEIGTH; row++)
            {
                Rows[row] = new Vic2SpriteRow();
            }
        }

        public class Vic2SpriteRow
        {
            public byte[] Bytes { get; set; } = new byte[DEFAULT_WIDTH / 8];
        }
    }

}
