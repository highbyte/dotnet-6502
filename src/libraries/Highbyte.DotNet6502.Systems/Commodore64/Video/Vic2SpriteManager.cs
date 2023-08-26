namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// Manager for all VIC-II sprites.
/// </summary>
public class Vic2SpriteManager
{
    public Vic2 Vic2 { get; private set; }

    public const int NUMBERS_OF_SPRITES = 8;
    //The Sprite top/left X position that appears on main screen (not border) position 0.
    public const int SCREEN_OFFSET_X = 24;
    //The Sprite top/left Y position that appears on main screen (not border) position 0.
    public const int SCREEN_OFFSET_Y = 50;
    public Vic2Sprite[] Sprites { get; private set; } = new Vic2Sprite[NUMBERS_OF_SPRITES];

    private Vic2SpriteManager() { }

    public Vic2SpriteManager(Vic2 vic2)
    {
        Vic2 = vic2;
        for (int i = 0; i < Sprites.Length; i++)
        {
            var sprite = new Vic2Sprite(i, this);
            Sprites[i] = sprite;
        }
    }
}
