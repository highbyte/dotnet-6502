using SkiaSharp;

namespace BlazorWasmSkiaTest.Skia
{
    public class SKPaintMaps
    {
        private readonly Dictionary<byte, SKPaint> _emulatorColorTextPaint;
        private readonly Dictionary<byte, SKPaint> _emulatorColorBackgroundPaint;

        public SKPaintMaps(int textSize, SKTypeface typeFace, Dictionary<byte, SKColor> colorMap)
        {
            _emulatorColorTextPaint = new Dictionary<byte, SKPaint>();
            _emulatorColorBackgroundPaint = new Dictionary<byte, SKPaint>();
            foreach (var colorKey in colorMap.Keys)
            {
                _emulatorColorTextPaint[colorKey] = BuildTextPaint(C64ColorMap[colorKey], textSize, typeFace);
                _emulatorColorBackgroundPaint[colorKey] = BuildBackgroundPaint(C64ColorMap[colorKey]);
            }
        }

        public SKPaint GetSKTextPaint(byte emulatorColor)
        {
            return _emulatorColorTextPaint[emulatorColor];
        }

        public SKPaint GetSKBackgroundPaint(byte emulatorColor)
        {
            return _emulatorColorBackgroundPaint[emulatorColor];
        }

        private SKPaint BuildTextPaint(SKColor color, int textSize, SKTypeface typeFace)
        {
            return new SKPaint
            {
                TextSize = textSize,
                Typeface = typeFace,
                //IsAntialias = true,
                Color = color,
                TextAlign = SKTextAlign.Left,
            };
        }
        private SKPaint BuildBackgroundPaint(SKColor color)
        {
            return new SKPaint
            {
                Color = color,
                Style = SKPaintStyle.Fill
            };
        }

        public static Dictionary<byte, SKColor> C64ColorMap = new()
        {
            { 0x00, new SKColor(0, 0, 0) },          // Black
            { 0x01, new SKColor(255, 255, 255) },    // White
            { 0x02, new SKColor(136, 0, 0) },        // Red
            { 0x03, new SKColor(170, 255, 238) },    // Cyan
            { 0x04, new SKColor(204, 68, 204) },     // Violet/purple
            { 0x05, new SKColor(0, 204, 85) },       // Green
            { 0x06, new SKColor(0, 0, 170) },        // Blue
            { 0x07, new SKColor(238, 238, 119) },    // Yellow
            { 0x08, new SKColor(221, 136, 185) },    // Orange
            { 0x09, new SKColor(102, 68, 0) },       // Brown
            { 0x0a, new SKColor(255, 119, 119) },    // Light red
            { 0x0b, new SKColor(51, 51, 51) },       // Dark grey
            { 0x0c, new SKColor(119, 119, 119) },    // Grey
            { 0x0d, new SKColor(170, 255, 102) },    // Light green
            { 0x0e, new SKColor(0, 136, 255) },      // Light blue
            { 0x0f, new SKColor(187, 187, 187) },    // Light grey
        };

    }
}
