using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using SkiaSharp;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64
{
    public class C64SkiaRenderer : IRenderer<C64>, IRenderer
    {
        private readonly SKCanvas _skCanvas;

        public int Width => Vic2.COLS * 8;
        public int Height => Vic2.ROWS * 8;

        public int MaxWidth => Vic2.PAL_PIXELS_PER_LINE_VISIBLE;
        public int MaxHeight => Vic2.PAL_LINES_VISIBLE;

        public C64SkiaRenderer(SKCanvas skCanvas)
        {
            _skCanvas = skCanvas;
        }

        public void Draw(C64 c64)
        {
            RenderBackgroundAndBorder(c64);
            RenderMainScreen(c64);
        }

        public void Draw(ISystem system)
        {
            Draw((C64)system);
        }

        private void RenderMainScreen(C64 c64)
        {
            var emulatorMem = c64.Mem;

            byte bgColor = emulatorMem[Vic2Addr.BACKGROUND_COLOR];

            // Build screen data characters based on emulator memory contents (byte)
            ushort currentScreenAddress = Vic2Addr.SCREEN_RAM_START;
            ushort currentColorAddress = Vic2Addr.COLOR_RAM_START;
            for (int row = 0; row < Vic2.ROWS; row++)
            {
                for (int col = 0; col < Vic2.COLS; col++)
                {
                    byte charByte = emulatorMem[currentScreenAddress++];
                    byte colorByte = emulatorMem[currentColorAddress++];
                    DrawEmulatorCharacterOnScreen(
                        col,
                        row,
                        charByte,
                        colorByte,
                        bgColor,
                        c64,
                        adjustForBorder: true
                        );
                }
            }
        }

        private void RenderBackgroundAndBorder(C64 c64)
        {
            var emulatorMem = c64.Mem;

            byte backgroundColor = emulatorMem[Vic2Addr.BACKGROUND_COLOR];
            byte borderColor = emulatorMem[Vic2Addr.BORDER_COLOR];

            SKColor backgroundSkColor = C64SkiaColors.NativeToSkColorMap[ColorMaps.C64ColorMap[backgroundColor]];
            SKColor borderSkColor = C64SkiaColors.NativeToSkColorMap[ColorMaps.C64ColorMap[borderColor]];

            // Draw 4 rectangles for border
            using (var paint = new SKPaint { Style = SKPaintStyle.Fill, Color = borderSkColor })
            {
                _skCanvas.DrawRect(0, 0, c64.VisibleWidth, c64.BorderHeight, paint);
                _skCanvas.DrawRect(0, (c64.BorderHeight + c64.Height), c64.VisibleWidth, c64.BorderHeight, paint);
                _skCanvas.DrawRect(0, c64.BorderHeight, c64.BorderWidth, c64.Height, paint);
                _skCanvas.DrawRect(c64.BorderWidth + c64.Width, c64.BorderHeight, c64.BorderWidth, c64.Height, paint);
            }
            // Draw 1 rectangles for background
            using (var paint = new SKPaint { Style = SKPaintStyle.Fill, Color = backgroundSkColor })
            {
                _skCanvas.DrawRect(c64.BorderWidth, c64.BorderHeight, c64.Width, c64.Height, paint);
            }

        }

        /// <summary>
        /// Draw character to screen, with adjusted position for border.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="emulatorCharacter"></param>
        /// <param name="emulatorFgColor"></param>
        /// <param name="emulatorBgColor"></param>
        public void DrawEmulatorCharacterOnScreen(int col, int row, byte emulatorCharacter, byte emulatorFgColor, byte emulatorBgColor, C64 c64, bool adjustForBorder)
        {
            int pixelPosX = col * c64.CharacterWidth;
            int pixelPosY = col * c64.CharacterHeight;
            if (adjustForBorder)
            {
                pixelPosX += c64.BorderWidth;
                pixelPosY += c64.BorderHeight;
            }

            // TODO: Draw character image from chargen ROM to a Skia surface

            // byte sadConsoleCharacter;
            // // Default to C64 screen codes as source
            // sadConsoleCharacter = TranslateC64ScreenCodeToSadConsoleC64Font(emulatorCharacter);

            // _getSadConsoleScreen().DrawCharacter(
            //     x,
            //     y,
            //     sadConsoleCharacter,
            //     C64SadConsoleColors.NativeToSadConsoleColorMap[ColorMaps.C64ColorMap[emulatorFgColor]],
            //     C64SadConsoleColors.NativeToSadConsoleColorMap[ColorMaps.C64ColorMap[emulatorBgColor]]
            //     );
        }
    }
}
