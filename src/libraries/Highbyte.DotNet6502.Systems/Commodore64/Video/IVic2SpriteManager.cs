namespace Highbyte.DotNet6502.Systems.Commodore64.Video
{
    public interface IVic2SpriteManager
    {
        public int SpritePointerStartAddress { get; }
        public int NumberOfSprites { get; }
        public int ScreenOffsetX { get; }
        public int ScreenOffsetY { get; }

        public Vic2Sprite[] Sprites { get; }

        public byte SpriteToSpriteCollisionStore { get; set; }
        public bool SpriteToSpriteCollisionIRQBlock { get; set; }

        public byte SpriteToBackgroundCollisionStore { get; set; }
        public bool SpriteToBackgroundCollisionIRQBlock { get; set; }

        public Vic2 Vic2 { get; }
        public void SetAllDirty();

        public void DetectChangesToSpriteData(ushort vic2Address, byte value);
        public void SetCollitionDetectionStatesAndIRQ();

        public byte GetSpriteToSpriteCollision();
        public byte GetSpriteToBackgroundCollision();

        public bool CheckCollisionAgainstBackground(Vic2Sprite sprite, int scrollX, int scrollY);

        public bool CheckCollision(ReadOnlySpan<byte> pixelData1, ReadOnlySpan<byte> pixelData2);

        public void GetSpriteRowLineData(Vic2Sprite sprite, int spriteScreenLine, ref Span<byte> spriteLineData);

        public void GetSpriteRowLineDataMatchingOtherSpritePosition(Vic2Sprite sprite0, Vic2Sprite sprite1, int sprite0ScreenLine, ref Span<byte> bytes);
        public void GetCharacterRowLineDataMatchingSpritePosition(Vic2Sprite sprite, int spriteScreenLine, int spriteBytesWidth, int scrollX, int scrollY, ref Span<byte> bytes);
    }
}
