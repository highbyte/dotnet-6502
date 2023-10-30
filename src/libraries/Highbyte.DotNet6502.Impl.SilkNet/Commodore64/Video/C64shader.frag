#version 330 core

in vec2 fViewPortPos;   // Remove if not needed
out vec4 FragColor;
uniform vec3 uWindowSize;
uniform vec2 uScale;
uniform vec2 uTextScreenStart;
uniform vec2 uTextScreenEnd;
uniform int uTextCharacterMode;

// Must be at least 16 bytes for UBO to work
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
struct ColorMapData 
{
  uint colorCode;   // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
  uint u1;          // unused   
  uint u2;          // unused   
  uint u3;          // unused   
  vec4 color;       // Shader color value
};
struct RasterLineData 
{
  uint borderColorCode;         // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
  uint backgroundColor0Code;    // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
  uint backgroundColor1Code;    // C64 color value 0-15. uint = 4 bytes, only using 1 byte. Used in Extended mode.
  uint backgroundColor2Code;    // C64 color value 0-15. uint = 4 bytes, only using 1 byte. Used in Extended mode. 

  uint backgroundColor3Code;    // C64 color value 0-15. uint = 4 bytes, only using 1 byte. Used in Extended mode. 
  uint u5;          // unused
  uint u6;          // unused
  uint u7;          // unused

  uint colMode40;   // 0 = 38 col mode, 1 = 40 col mode
  uint rowMode25;   // 0 = 24 row mode, 1 = 25 row mode
  uint scrollX;     // 0 to 7 horizontal fine scrolling (+1 in 38 col mode). Default 0.
  int scrollY;      // -3 to 4 vertical fine scrolling (+1 in 24 row mode). Default 0.
};
struct SpriteData 
{
  uint visible;     // 0 = not visible, 1 = visible 
  uint x;           // uint = 4 bytes, only using 2 bytes
  uint y;           // uint = 4 bytes, only using 2 bytes
  uint color;       // uint = 4 bytes, only using 1 byte
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
layout (std140) uniform ubRasterLineData
{ 
  RasterLineData uRasterLineData[312]; // Maximum used by any VIC2 chip (PAL?)
};
layout (std140) uniform ubSpriteData
{ 
  SpriteData uSpriteData[8]; // 8 sprites
};
layout (std140) uniform ubSpriteContentData
{ 
  SpriteContentData uSpriteContentData[8*3*21];   // 3 items (24 pixels) per row * 21 rows * 8 sprites = 63 items * 8 sprites = 504 items
};

const int TextMode_Standard = 0;
const int TextMode_Extended = 1;
const int TextMode_MultiColor = 2;

// Returns true if pixel is foreground, false if background.
// If non-background color is used, pixelColor is not set.
bool GetStandardAndExtendedColor(uint charLine, uint charsetBitPosition, vec4 charColor, vec4 bgColor, out vec4 pixelColor)
{
    // Select color for pixel
    uint mask = 1u << charsetBitPosition;
    if((charLine & mask) == mask)
    {
        pixelColor = charColor;
        return true;
    }
    else
    {
        pixelColor = bgColor;
        return false;
    }
}

// Returns true if pixel is foreground, false if background.
// If non-background color is used, pixelColor is not set.
bool GetMultiColor(uint charLine, uint charsetBitPosition, vec4 charColor, vec4 bgColor0, vec4 bgColor1, vec4 bgColor2, out vec4 pixelColor)
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
                pixelColor = bgColor0;
                return false;
            case 1u:
                pixelColor = bgColor1;
                return true;
            case 2u:
                pixelColor =  bgColor2;
                return true;
            case 3u:
                pixelColor = charColor;
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
    uint horizontalBorderOffset = uRasterLineData[y].colMode40 == 1u ? 0u : 8u;
    uint verticalBorderOffset = uRasterLineData[y].rowMode25 == 1u ? 0u : 4u;

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

	uint screenx = x - uint(uTextScreenStart.x + uRasterLineData[y].scrollX);
	uint screeny = y - uint(uTextScreenStart.y + uRasterLineData[y].scrollY);
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
    uint bgColorCode0 = uRasterLineData[y].backgroundColor0Code;
    vec4 bgColor0 = uColorMapData[bgColorCode0 & 15u].color;

    uint charColorCode = uTextData[screenMemIndex].color;

    bool isForeground;
    if(uTextCharacterMode == TextMode_Standard)
    {
        vec4 charColor = uColorMapData[charColorCode & 15u].color;
        vec4 bgColor = bgColor0;
        isForeground = GetStandardAndExtendedColor(charLine, charsetBitPosition, charColor, bgColor, pixelColor);
    }
    else if(uTextCharacterMode == TextMode_Extended)
    {
        vec4 charColor = uColorMapData[charColorCode & 15u].color;

        // Bit 6 and 7 of character byte is used to select background color (0-3)
        int bgColorSelector = int(uTextData[screenMemIndex].character) >> 6;
        vec4 bgColor;
        switch (bgColorSelector) {
        case 0:
            bgColor = bgColor0;
            break;
        case 1:
            uint bgColorCode1 = uRasterLineData[y].backgroundColor1Code;
            bgColor = uColorMapData[bgColorCode1 & 15u].color;
            break;
        case 2:
            uint bgColorCode2 = uRasterLineData[y].backgroundColor2Code;
            bgColor = uColorMapData[bgColorCode2 & 15u].color;
            break;
        case 3:
            uint bgColorCode3 = uRasterLineData[y].backgroundColor3Code;
            bgColor = uColorMapData[bgColorCode3 & 15u].color;
            break;
        default:
            bgColor = uColorMapData[0].color;
            break;
        }

        isForeground = GetStandardAndExtendedColor(charLine, charsetBitPosition, charColor, bgColor, pixelColor);
    }
    else if(uTextCharacterMode == TextMode_MultiColor)
    {
        // When in MultiColor mode, a character can still be displayed in Standard mode depending on the value from color RAM.
        if(charColorCode <= 7u)
        {   
            // If color RAM value is 0-7, normal Standard mode is used (not multi-color)
            vec4 charColor = uColorMapData[charColorCode & 15u].color;
            isForeground = GetStandardAndExtendedColor(charLine, charsetBitPosition, charColor, bgColor0, pixelColor);
        }
        else
        {
            // If displaying in MultiColor mode, the actual color used from color RAM will be values 0-7.
            // Thus color values 8-15 are transformed to 0-7
            charColorCode = ((charColorCode & 15u) - 8u);
            vec4 charColor = uColorMapData[charColorCode & 15u].color;

            uint bgColorCode1 = uRasterLineData[y].backgroundColor1Code;
            vec4 bgColor1 = uColorMapData[bgColorCode1 & 15u].color;

            uint bgColorCode2 = uRasterLineData[y].backgroundColor2Code;
            vec4 bgColor2 = uColorMapData[bgColorCode2 & 15u].color;

            isForeground = GetMultiColor(charLine, charsetBitPosition, charColor, bgColor0, bgColor1, bgColor2, pixelColor);
        }
    }

    return isForeground;
}

bool GetSpritePixelColor(uint x, uint y, bool prioOverForground, out vec4 pixelColor)
{
    // Sprite/sprite priority is first sprite 0 (will be drawn over others), then 1, etc to 7. 
    for(uint i=0u; i<8u; i++)
    {
        if(uSpriteData[i].visible == 0u)
            continue;

        // Check if x/y position possibly is covered by sprite
        uint spriteX = uSpriteData[i].x;
        uint spriteY = uSpriteData[i].y;
        uint spriteWidth = 24u;  // TODO: Read from UBO. Is width adjusted for double, or should that be done here?
        uint spriteHeight = 21u; // TODO: Read from UBO. Is height adjusted for double, or should that be done here?
        if( !(x>=spriteX && x<(spriteX+spriteWidth) && y>=spriteY && y<(spriteY+spriteHeight)) )
            continue;

        // Check if sprite pixel is set
        uint dx = x - spriteX;
        uint dy = y - spriteY;

        uint spriteContentByteIndexStart = i * 3u * 21u;   // i = sprite number, 3 = 3 bytes per row, 21 = 21 rows
        uint byteIndex = (dy * 3u) + (dx / 8u);
        uint bytePixelPosition = dx % 8u;
        uint lineData = uSpriteContentData[spriteContentByteIndexStart + byteIndex].content;
        uint mask = 1u << (7u - bytePixelPosition);
        if((lineData & mask) != mask)
            continue;

        uint colorCode = uSpriteData[i].color;
        pixelColor = uColorMapData[colorCode & 15u].color;
        return true;
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

    // PRIO 2: Text screen pixels that are not background color
    bool border;
    vec4 textModePixelColor;
    bool textModePixelSet = GetTextModePixelColor(x, y, border, textModePixelColor);
    if(textModePixelSet)
    {
        FragColor = textModePixelColor;
        return;
    }

    // PRIO 3: Get pixel from sprites that do not have priority over foreground (but will show above background color)
    vec4 nonPrioSpritePixelColor;
    bool nonPrioSpritePixelSet = GetSpritePixelColor(x, y, false, nonPrioSpritePixelColor);
    if(nonPrioSpritePixelSet)
    {
        FragColor = nonPrioSpritePixelColor;
        return;
    }

    // PRIO 4: Either text mode background color or border color
    if(!border)
    {
        // textModePixelColor will contain the text mode background color here
        FragColor = textModePixelColor;
        return;
    }

    // Default to border color if neither text screen or sprite pixel was set
    uint borderColorCode = uRasterLineData[y].borderColorCode;
    vec4 borderColor = uColorMapData[borderColorCode & 15u].color;
    FragColor = borderColor;
}