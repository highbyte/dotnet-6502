using System.Numerics;

namespace Highbyte.DotNet6502.Systems.Commodore64.Render.CustomPayload;

// Types typically used for Uniform Buffer Objects, must align to 16 bytes.
public struct TextData
{
    public uint Character;  // uint = 4 bytes, only using 1 byte
    public uint Color;      // uint = 4 bytes, only using 1 byte
    public uint _;          // unused
    public uint __;         // unused
}
public struct CharsetData
{
    public uint CharLine;   // uint = 4 bytes, only using 1 byte
    public uint _;          // unused
    public uint __;         // unused
    public uint ___;        // unused
}
public struct BitmapData
{
    //public fixed uint Lines[8];   // Note: Could not get uint array inside struct to work in UBO from .NET (even with C# unsafe fixed arrays).
    public uint Line0;
    public uint Line1;
    public uint Line2;
    public uint Line3;
    public uint Line4;
    public uint Line5;
    public uint Line6;
    public uint Line7;

    public uint BackgroundColorCode;   // C64 color value 0-15. uint = 4 bytes, only using 1 byte. From text screen ram low nybble.
    public uint ForegroundColorCode;   // C64 color value 0-15. uint = 4 bytes, only using 1 byte. From text screen ram high nybble.
    public uint ColorRAMColorCode;     // C64 color value 0-15. uint = 4 bytes, only using 1 byte. From color RAM (low nybble).
    public uint __;          // unused 

}
public struct ColorMapData
{
    public uint ColorCode;  // uint = 4 bytes, only using 1 byte
    public uint __;         // unused
    public uint ___;        // unused
    public uint ____;       // unused
    public Vector4 Color;   // Vector4 = 16 bytes
}
public struct ScreenLineData
{
    public uint BorderColorCode;        // uint = 4 bytes, only using 1 byte
    public uint BackgroundColor0Code;   // uint = 4 bytes, only using 1 byte
    public uint BackgroundColor1Code;   // uint = 4 bytes, only using 1 byte
    public uint BackgroundColor2Code;   // uint = 4 bytes, only using 1 byte

    public uint BackgroundColor3Code;   // uint = 4 bytes, only using 1 byte
    public uint SpriteMultiColor0;      // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
    public uint SpriteMultiColor1;      // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
    public uint _______;    // unused

    public uint Sprite0ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
    public uint Sprite1ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
    public uint Sprite2ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
    public uint Sprite3ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 

    public uint Sprite4ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
    public uint Sprite5ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
    public uint Sprite6ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
    public uint Sprite7ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 

    public uint ColMode40;   // 0 = 38 col mode, 1 = 40 col mode
    public uint RowMode25;   // 0 = 24 row mode, 1 = 25 row mode
    public uint ScrollX;     // 0 to 7 horizontal fine scrolling (+1 in 38 col mode)
    public int ScrollY;      // -3 to 4 vertical fine scrolling (+1 in 24 row mode)
}
public struct SpriteData
{
    public uint Visible;    // 0 = not visible, 1 = visible 
    public int X;           // int = 4 bytes, only using 2 bytes
    public int Y;           // int = 4 bytes, only using 2 bytes
    public uint Color;      // uint = 4 bytes, only using 1 byte

    public uint DoubleWidth; // 0 = Normal width, 1 = Double width
    public uint DoubleHeight;// 0 = Normal height, 1 = Double height
    public uint PriorityOverForeground; // 0 = No priority, 1 = Priority over foreground
    public uint MultiColor;  // 0 = Single color mode, 1 = MultiColor mode
}
public struct SpriteContentData
{
    public uint Content;  // 3 bytes (24 pixels) per row * 21 rows = 63 items.
    public uint __;       // unused
    public uint ___;      // unused 
    public uint ____;     // unused  
}
