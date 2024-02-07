#version 330 core

in vec2 fViewPortPos;   // Remove if not needed
out vec4 FragColor;
uniform vec3 uWindowSize;
uniform vec2 uScale;
uniform int uDisplayMode;       // 0 = Text mode, 1 = Bitmap mode
uniform vec2 uTextScreenStart;  // Pixel coordinate for main (not border) screen start. Fine scroll not included.
uniform vec2 uTextScreenEnd;    // Pixel coordinate for main (not border) screen end. Fine scroll not included.
uniform int uTextCharacterMode; // 0 = Standard, 1 = Extended, 2 = MultiColor
uniform int uBitmapMode;        // 0 = Standard, 1 = MultiColor

// When struct is used in a UBO it must be multiple of 16 bytes. One uint = 4 bytes.
struct TextData 
{
  uint character;   // C64 screen character code 0-255. uint = 4 bytes, only using 1 byte. 
  uint color;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte
  uint u2;          // unused
  uint u3;          // unused
};
struct CharsetData 
{
  uint charline;    // C64 character line (8 bits = 8 pixels). uint = 4 bytes, only using 1 byte
  uint u1;          // unused
  uint u2;          // unused
  uint u3;          // unused
};
struct BitmapData 
{
//    uint lines[8];    // Note: Could not get uint array inside struct to work in UBO from .NET (even with C# unsafe fixed arrays).
    uint line0; // 8 pixels ( = 8 bits  = 1 byte ) used per line
    uint line1;
    uint line2; 
    uint line3;
    uint line4;
    uint line5;
    uint line6;
    uint line7;

    uint backgroundColorCode;   // C64 color value 0-15. uint = 4 bytes, only using 1 byte.
    uint foregroundColorCode;   // C64 color value 0-15. uint = 4 bytes, only using 1 byte.
    uint u1;          // unused
    uint u2;          // unused 
};
struct ColorMapData 
{
  uint colorCode;   // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
  uint u1;          // unused   
  uint u2;          // unused   
  uint u3;          // unused   
  vec4 color;       // Shader color value
};
struct ScreenLineData 
{
  uint borderColorCode;         // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
  uint backgroundColor0Code;    // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
  uint backgroundColor1Code;    // C64 color value 0-15. uint = 4 bytes, only using 1 byte. Used in Extended mode.
  uint backgroundColor2Code;    // C64 color value 0-15. uint = 4 bytes, only using 1 byte. Used in Extended mode. 

  uint backgroundColor3Code;    // C64 color value 0-15. uint = 4 bytes, only using 1 byte. Used in Extended mode. 
  uint spriteMultiColor0;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
  uint spriteMultiColor1;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
  uint u7;                      // unused

  uint sprite0ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
  uint sprite1ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
  uint sprite2ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
  uint sprite3ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 

  uint sprite4ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
  uint sprite5ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
  uint sprite6ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
  uint sprite7ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte.

  uint colMode40;   // 0 = 38 col mode, 1 = 40 col mode
  uint rowMode25;   // 0 = 24 row mode, 1 = 25 row mode
  uint scrollX;     // 0 to 7 horizontal fine scrolling (+1 in 38 col mode). Default 0.
  int scrollY;      // -3 to 4 vertical fine scrolling (+1 in 24 row mode). Default 0.
};
struct SpriteData 
{
  uint visible;     // 0 = not visible, 1 = visible 
  int x;            // int = 4 bytes, only using 2 bytes
  int y;            // int = 4 bytes, only using 2 bytes
  uint color;       // uint = 4 bytes, only using 1 byte

  uint doubleWidth; // 0 = Normal width, 1 = Double width
  uint doubleHeight;// 0 = Normal height, 1 = Double height
  uint priorityOverForeground; // 0 = No priority, 1 = Priority over foreground
  uint multiColor;  // 0 = Single color mode, 1 = MultiColor mode
};
struct SpriteContentData 
{
  uint content;     // 1 byte. An entire sprite consists of 3 items (24 pixels) per row * 21 rows = 63 items
  uint u1;          // unused
  uint u2;          // unused
  uint u3;          // unused
};

// Note: need total 16 bytes for each uniform value. Either a vec4, or a custom struct that adds up to 16.
layout (std140) uniform ubTextData
{ 
  TextData uTextData[1024]; 
};
layout (std140) uniform ubCharsetData
{ 
  CharsetData uCharsetData[2048]; 
};
layout (std140) uniform ubColorMap
{ 
  ColorMapData uColorMapData[16];
};
layout (std140) uniform ubScreenLineData
{ 
  ScreenLineData uScreenLineData[312]; // Maximum used by any VIC2 chip (PAL?)
};
layout (std140) uniform ubSpriteData
{ 
  SpriteData uSpriteData[8]; // 8 sprites
};
layout (std140) uniform ubSpriteContentData
{ 
  SpriteContentData uSpriteContentData[8*3*21];   // 3 items (24 pixels) per row * 21 rows * 8 sprites = 63 items * 8 sprites = 504 items
};
layout (std140) uniform ubBitmapData
{ 
// Total UBO size 1000 (40*25 "characters") * 8 (bytes per "character") * 4 (bytes per uint) = 32000 bytes
  BitmapData uBitmapData[1000];
};


const int DisplayMode_Text = 0;
const int DisplayMode_Bitmap = 1;

const int TextMode_Standard = 0;
const int TextMode_Extended = 1;
const int TextMode_MultiColor = 2;

const int BitmapMode_Standard = 0;
const int BitmapMode_MultiColor = 1;

// Returns true if pixel is foreground, false if background.
bool IsSinglePixelSet(uint charLine, uint pixelBitPosition)
{
    // Check if a forground pixel is set
    uint mask = 1u << pixelBitPosition;
    if((charLine & mask) == mask)
        return true;
    else
        return false;
}

// Returns true if pixel is foreground, false if background.
// If non-background color is used, pixelColor is not set based on 2 bit pattern
bool GetMultiColor(uint charLine, uint charsetBitPosition, vec4 color01, vec4 color10, vec4 color11, out vec4 pixelColor)
{
        uint mask;
        uint value;
        if(charsetBitPosition == 0u || charsetBitPosition == 1u)
        {
            mask = 3u;
            value = charLine & mask;
        }
        else if(charsetBitPosition == 2u || charsetBitPosition == 3u)
        {
            mask = 12u;
            value = (charLine & mask) >> 2;
        }
        else if(charsetBitPosition == 4u || charsetBitPosition == 5u)
        {
            mask = 48u;
            value = (charLine & mask) >> 4; 
        }
        else if(charsetBitPosition == 6u || charsetBitPosition == 7u)
        {
            mask = 192u;
            value = (charLine & mask) >> 6;
        }

        switch(value)
        {
            case 0u:
                return false;
            case 1u:
                pixelColor = color01;
                return true;
            case 2u:
                pixelColor = color10;
                return true;
            case 3u:
                pixelColor = color11;
                return true;
            default:
                pixelColor = vec4(0,1,0,1);    // Shouldn't happen
                return false;
        }
}

// Return values description:
// If x,y is in border:
//  - border is set to true.
//  - pixelColor is not set.
//  - return value is false;
//
// If x,y is inside text screen:
//  - border is set to false.
//  - pixelColor is set to the color of the pixel.
//  - return value is true if color is NOT the background color.
//  - return value is false if color is the background color.
//
bool GetTextModePixelColor(uint x, uint y, out bool border, out vec4 pixelColor)
{
    // Detect border area, draw only border color
    uint horizontalBorderOffset = uScreenLineData[y].colMode40 == 1u ? 0u : 8u;
    uint verticalBorderOffset = uScreenLineData[y].rowMode25 == 1u ? 0u : 4u;

    // Workaround for 38 col mode (to counter fine x scroll value is set to +1 in 38 col mode)
    if(horizontalBorderOffset!=0u)
        x=x+1u;
    // Workaround for 24 row mode (to counter fine y scroll value is set to +1 in 24 row mode)
    if(verticalBorderOffset!=0u)
        y=y+1u;

    if((x < (uTextScreenStart.x+horizontalBorderOffset) || (x > (uTextScreenEnd.x-horizontalBorderOffset)))
         || (y < (uTextScreenStart.y+verticalBorderOffset) || (y > (uTextScreenEnd.y-verticalBorderOffset))) )
    {
        border = true;
        return false;
    }

    border = false;

	uint screenx = x - uint(uTextScreenStart.x + uScreenLineData[y].scrollX);
	uint screeny = y - uint(uTextScreenStart.y + uScreenLineData[y].scrollY);
	int col = int(screenx) / 8;
	int row = int(screeny) / 8;
	int screenMemIndex = (row * 40) + col;

    // TODO: Shouldn't occur, remove?
    if(screenMemIndex >= uTextData.length)
        return false;

    // Get character code
    uint charCode = uTextData[screenMemIndex].character;
    if(uTextCharacterMode == TextMode_Extended)
    {
        // In Extened mode, the actual usable character codes are in the lower 6 bits (0-63)
        charCode = charCode & 63u; 
    }

    // Get character line from charset definition
    uint charsetIndex = charCode << 3;  // Each character is 8 bytes (left shift 3 times = * 8), 1 byte/line
    uint charsetLineIndex = screeny % 8u;
    uint charsetBitPosition = ((7u - screenx) % 8u);
    uint charLine = uCharsetData[charsetIndex + charsetLineIndex].charline;

    // Select colors
    uint bgColorCode0 = uScreenLineData[y].backgroundColor0Code;
    vec4 bgColor0 = uColorMapData[bgColorCode0 & 15u].color;

    uint charColorCode = uTextData[screenMemIndex].color;
    vec4 charColor = uColorMapData[charColorCode & 15u].color;

    bool isForeground;
    if(uTextCharacterMode == TextMode_Standard)
    {
        isForeground = IsSinglePixelSet(charLine, charsetBitPosition);
        if(isForeground)
            pixelColor = charColor;
        else
            pixelColor = bgColor0;
    }
    else if(uTextCharacterMode == TextMode_Extended)
    {
        // Bit 6 and 7 of character byte is used to select background color (0-3)
        int bgColorSelector = int(uTextData[screenMemIndex].character) >> 6;
        vec4 bgColor;
        switch (bgColorSelector) {
        case 0:
            bgColor = bgColor0;
            break;
        case 1:
            uint bgColorCode1 = uScreenLineData[y].backgroundColor1Code;
            bgColor = uColorMapData[bgColorCode1 & 15u].color;
            break;
        case 2:
            uint bgColorCode2 = uScreenLineData[y].backgroundColor2Code;
            bgColor = uColorMapData[bgColorCode2 & 15u].color;
            break;
        case 3:
            uint bgColorCode3 = uScreenLineData[y].backgroundColor3Code;
            bgColor = uColorMapData[bgColorCode3 & 15u].color;
            break;
        default:
            bgColor = uColorMapData[0].color;
            break;
        }

        isForeground = IsSinglePixelSet(charLine, charsetBitPosition);
        if(isForeground)
            pixelColor = charColor;
        else
            pixelColor = bgColor;
        
    }
    else if(uTextCharacterMode == TextMode_MultiColor)
    {
        // When in MultiColor mode, a character can still be displayed in Standard mode depending on the value from color RAM.
        if(charColorCode <= 7u)
        {   
            // If color RAM value is 0-7, normal Standard mode is used (not multi-color)
            isForeground = IsSinglePixelSet(charLine, charsetBitPosition);
            if(isForeground)
                pixelColor = charColor;
            else
                pixelColor = bgColor0;
        }
        else
        {
            // If displaying in MultiColor mode, the actual color used from color RAM will be values 0-7.
            // Thus color values 8-15 are transformed to 0-7
            uint charColorCode2 = ((charColorCode & 15u) - 8u);
            vec4 charColor2 = uColorMapData[charColorCode2 & 15u].color;

            uint bgColorCode1 = uScreenLineData[y].backgroundColor1Code;
            vec4 bgColor1 = uColorMapData[bgColorCode1 & 15u].color;

            uint bgColorCode2 = uScreenLineData[y].backgroundColor2Code;
            vec4 bgColor2 = uColorMapData[bgColorCode2 & 15u].color;

            isForeground = GetMultiColor(charLine, charsetBitPosition, bgColor1, bgColor2, charColor2, pixelColor);
            if(!isForeground)
                pixelColor = bgColor0;
        }
    }

    return isForeground;
}

// Return values description:
// If x,y is in border:
//  - border is set to true.
//  - pixelColor is not set.
//  - return value is false;
//
// If x,y is inside text screen:
//  - border is set to false.
//  - pixelColor is set to the color of the pixel.
//  - return value is true if color is NOT the background color.
//  - return value is false if color is the background color.
//
bool GetBitmapModePixelColor(uint x, uint y, out bool border, out vec4 pixelColor)
{
    // Detect border area, draw only border color
    uint horizontalBorderOffset = uScreenLineData[y].colMode40 == 1u ? 0u : 8u;
    uint verticalBorderOffset = uScreenLineData[y].rowMode25 == 1u ? 0u : 4u;

    // Workaround for 38 col mode (to counter fine x scroll value is set to +1 in 38 col mode)
    if(horizontalBorderOffset!=0u)
        x=x+1u;
    // Workaround for 24 row mode (to counter fine y scroll value is set to +1 in 24 row mode)
    if(verticalBorderOffset!=0u)
        y=y+1u;

    if((x < (uTextScreenStart.x+horizontalBorderOffset) || (x > (uTextScreenEnd.x-horizontalBorderOffset)))
         || (y < (uTextScreenStart.y+verticalBorderOffset) || (y > (uTextScreenEnd.y-verticalBorderOffset))) )
    {
        border = true;
        return false;
    }

    border = false;

	uint screenx = x - uint(uTextScreenStart.x + uScreenLineData[y].scrollX);
	uint screeny = y - uint(uTextScreenStart.y + uScreenLineData[y].scrollY);

	uint col = screenx / 8u;
	uint row = screeny / 8u;
	uint charOffset = (row * 40u) + col;
    uint line = screeny % 8u;
    uint bitPosition = 7u - (screenx % 8u);

    uint bitmapLine;
    switch(line)
    {
        case 0u:
            bitmapLine = uBitmapData[charOffset].line0;
            break;
        case 1u:
            bitmapLine = uBitmapData[charOffset].line1;
            break;
        case 2u:
            bitmapLine = uBitmapData[charOffset].line2;
            break;
        case 3u:
            bitmapLine = uBitmapData[charOffset].line3;
            break;
        case 4u:
            bitmapLine = uBitmapData[charOffset].line4;
            break;
        case 5u:
            bitmapLine = uBitmapData[charOffset].line5;
            break;
        case 6u:
            bitmapLine = uBitmapData[charOffset].line6;
            break;
        case 7u:
            bitmapLine = uBitmapData[charOffset].line7;
            break;
        default:
            break;
    }

    uint bgColorCode0 = uScreenLineData[y].backgroundColor0Code;
    vec4 bgColor0 = uColorMapData[bgColorCode0 & 15u].color;

    uint bitmapFgColorCode = uBitmapData[charOffset].foregroundColorCode;
    vec4 bitmapFgColor = uColorMapData[bitmapFgColorCode & 15u].color;

    uint bitmapBgColorCode = uBitmapData[charOffset].backgroundColorCode;
    vec4 bitmapBgColor = uColorMapData[bitmapBgColorCode & 15u].color;

    bool isForeground;
    if(uBitmapMode == BitmapMode_Standard)
    {
        isForeground = IsSinglePixelSet(bitmapLine, bitPosition);

        if(isForeground)
            pixelColor = bitmapFgColor;
        else
            pixelColor = bitmapBgColor;
    }
    else if(uBitmapMode == BitmapMode_MultiColor)
    {
        isForeground = true;           // TEST 
        pixelColor = vec4(1,0,0,1);    // TEST
    }
    return isForeground;
}

bool GetSpritePixelColor(uint x, uint y, bool prioOverForground, out vec4 pixelColor)
{
    // Detect border area, don't draw sprites there (TODO: support opening border for sprites?) 
    uint horizontalBorderOffset = uScreenLineData[y].colMode40 == 1u ? 0u : 8u;
    uint verticalBorderOffset = uScreenLineData[y].rowMode25 == 1u ? 0u : 4u;

    // Workaround for 38 col mode (to counter fine x scroll value is set to +1 in 38 col mode)
    if(horizontalBorderOffset!=0u)
        x=x+1u;
    // Workaround for 24 row mode (to counter fine y scroll value is set to +1 in 24 row mode)
    if(verticalBorderOffset!=0u)
        y=y+1u;

    if((x < (uTextScreenStart.x+horizontalBorderOffset) || (x > (uTextScreenEnd.x-horizontalBorderOffset)))
         || (y < (uTextScreenStart.y+verticalBorderOffset) || (y > (uTextScreenEnd.y-verticalBorderOffset))) )
    {
        return false;
    }

    // Sprite/sprite priority is first sprite 0 (will be drawn over others), then 1, etc to 7. 
    for(int i=0; i<8; i++)
    {
        // Only process visible sprites
        if(uSpriteData[i].visible == 0u)
            continue;

        // Only process sprites that matches the prioOverForground input parameter
        if(uSpriteData[i].priorityOverForeground == 1u && !prioOverForground)
            continue;
        if(uSpriteData[i].priorityOverForeground == 0u && prioOverForground)
            continue;

        // Check if x/y position possibly is covered by sprite boundaries
        int xi = int(x);
        int yi = int(y);

        int spriteX = uSpriteData[i].x;
        int spriteY = uSpriteData[i].y;
        int scaleFactorX = uSpriteData[i].doubleWidth == 0u ? 1 : 2;
        int scaleFactorY = uSpriteData[i].doubleHeight == 0u ? 1 : 2;
        const int defaultSpriteWidth = 24;  // TODO: Read from UBO or uniform?
        const int defaultSpriteHeight = 21; // TODO: Read from UBO or uniform?
        if( ! (xi>=spriteX && xi<(spriteX+(defaultSpriteWidth*scaleFactorX)) 
            && yi>=spriteY && yi<(spriteY+(defaultSpriteHeight*scaleFactorY))) )
            continue;

        // Check if sprite pixel is set
        int dx = (xi - spriteX) / scaleFactorX;
        int dy = (yi - spriteY) / scaleFactorY;
        if(dx<0 || dy<0)
            continue;

        int spriteContentByteIndexStart = i * 3 * 21;   // i = sprite number, 3 = 3 bytes per row, 21 = 21 rows
        int byteIndex = (dy * 3) + (dx / 8);
        int bytePixelPosition = dx % 8;
        uint lineData = uSpriteContentData[spriteContentByteIndexStart + byteIndex].content;

        if(uSpriteData[i].multiColor == 0u)
        {
            bool foreground = IsSinglePixelSet(lineData, uint(7-bytePixelPosition));
            if(!foreground)
                continue;

            uint colorCode = uSpriteData[i].color;
            pixelColor = uColorMapData[colorCode & 15u].color;
            return true;
        }
        else
        {
            bool foreground = GetMultiColor(
                lineData,                 
                uint(7 - bytePixelPosition), 
                uColorMapData[uScreenLineData[y].spriteMultiColor0 & 15u].color, 
                uColorMapData[uSpriteData[i].color & 15u].color, 
                uColorMapData[uScreenLineData[y].spriteMultiColor1 & 15u].color, 
                pixelColor);
                
            if(!foreground)
                continue;
            return true;
        }

    }

    return false;
}

void main()
{
    // Screen coordinate option #1, via built-in gl_FragCoord
    // gl_FragCoord has absolute screen coordinates, not affected by window size.
    uint x = uint(gl_FragCoord.x * 1/uScale.x);
    uint y = uint((uWindowSize.y - gl_FragCoord.y) * 1/uScale.y); // Make sure top/left is 0,0

    // Screen coordinate option #2, via input variable from vertex shader
    // fViewPortPos (passed from vertex shader) has viewport coordinates, affected by window sixe.
    // Change viewport ranges from -1 to +1, to 0 to 1.
    //    float viewX = (fViewPortPos.x + 1) / 2;
    //    float viewY = 1 - ((fViewPortPos.y + 1) / 2);  // Make sure top/left is 0,0
    //    uint x = uint(viewX * uWindowSize.x * 1/uScale.x);
    //    uint y = uint(viewY * uWindowSize.y * 1/uScale.y);

    // PRIO 1: Get pixel from sprites that have priority over foreground
    vec4 prioSpritePixelColor;
    bool prioSpritePixelSet = GetSpritePixelColor(x, y, true, prioSpritePixelColor);
    if(prioSpritePixelSet)
    {
        FragColor = prioSpritePixelColor;
        return;
    }

    // PRIO 2: Text or bitmap screen pixels that are not background color
    bool border;
    vec4 pixelColor;

    if(uDisplayMode == DisplayMode_Text)
    {
        bool textModePixelSet = GetTextModePixelColor(x, y, border, pixelColor);
        if(textModePixelSet)
        {
            FragColor = pixelColor;
            return;
        }
    }
    else // Assume uDisplayMode == DisplayMode_Bitmap
    {
        bool bitmapModePixelSet = GetBitmapModePixelColor(x, y, border, pixelColor);
        if(bitmapModePixelSet)
        {
            FragColor = pixelColor;
            return;
        }
    }

    // PRIO 3: Get pixel from sprites that do not have priority over foreground (but will show above background color)
    vec4 nonPrioSpritePixelColor;
    bool nonPrioSpritePixelSet = GetSpritePixelColor(x, y, false, nonPrioSpritePixelColor);
    if(nonPrioSpritePixelSet)
    {
        FragColor = nonPrioSpritePixelColor;
        return;
    }

    // PRIO 4: Either background color or border color
    if(!border)
    {
        // pixelColor will contain the background color here
        FragColor = pixelColor;
        return;
    }

    // Default to border color if neither text screen or sprite pixel was set
    uint borderColorCode = uScreenLineData[y].borderColorCode;
    vec4 borderColor = uColorMapData[borderColorCode & 15u].color;
    FragColor = borderColor;
}