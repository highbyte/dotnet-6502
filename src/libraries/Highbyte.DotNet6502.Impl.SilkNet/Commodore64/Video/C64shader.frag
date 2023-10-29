#version 330 core

in vec2 fViewPortPos;   // Remove if not needed
out vec4 FragColor;
uniform vec3 uWindowSize;
uniform vec2 uScale;
uniform vec2 uTextScreenStart;
uniform vec2 uTextScreenEnd;
uniform uint uBorderColor0;
uniform uint uBgColor;

// Must be at least 16 bytes for UBO to work
struct TextData 
{
  uint character;   // C64 screen character code 0-255. uint = 4 bytes, only using 1 byte. 
  uint color;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte
  uint u3;          // unused
  uint u4;          // unused
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
  uint u2;          // unused   
  uint u3;          // unused   
  uint u4;          // unused   
  vec4 color;       // Shader color value
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

//uint borderStartX = uint(49);
//uint borderStartY = uint(17);
//uint screenWidth = uint(320);
//uint screenHeight = uint(200);

//vec4 bgColor = vec4(0.5, 0.5, 0.5, 1.0);
//vec4 fgColor = vec4(0.2, 0.2, 0.8, 1.0);
//
const uint u1 = uint(1);
const uint u7 = uint(7);
const uint u8 = uint(8);
const uint u15 = uint(15);
const uint u40 = uint(40);

void main()
{
    vec4 borderColor0 = uColorMapData[uBorderColor0 & u15].color;
    FragColor = borderColor0;

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

    // Don't draw any pixels in the border
    if((x < uTextScreenStart.x || (x >= uTextScreenEnd.x))
         || (y < uTextScreenStart.y || (y >= uTextScreenEnd.y)) )
      return;

	uint screenx = x - uint(uTextScreenStart.x);    //TODO: uTextScreenStart is adjusted for 38/40 col mode. Here it should alwayws use 40 col start positon 
	uint screeny = y - uint(uTextScreenStart.y);    //TODO: uTextScreenStart is adjusted for 24/25 row mode. Here it should alwayws use 25 row start positon
	int col = int(screenx) / 8;
	int row = int(screeny) / 8;
	int screenMemIndex = (row * 40) + col;

    // TODO: Shouldn't occur, remove?
    if(screenMemIndex >= uTextData.length)
        return;

    vec4 bgColor = uColorMapData[uBgColor & u15].color;
    uint charColorCode = uTextData[screenMemIndex].color;
    vec4 fgColor = uColorMapData[charColorCode & u15].color;

    uint charCode = uTextData[screenMemIndex].character;
    uint charsetIndex = charCode << 3;  // Each character is 8 bytes (left shift 3 times = * 8), 1 byte/line
    uint charsetLineIndex = screeny % u8;
    uint charsetBitPosition = ((u7 - screenx) % u8);
    uint charLine = uCharsetData[charsetIndex + charsetLineIndex].charline;

    uint mask = u1 << charsetBitPosition;
    if((charLine & mask) == mask)
        FragColor = fgColor;
    else
        FragColor = bgColor;
}