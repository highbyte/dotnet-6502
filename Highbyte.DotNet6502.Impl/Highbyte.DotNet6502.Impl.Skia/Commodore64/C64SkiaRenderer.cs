using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using SkiaSharp;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64
{
    public class C64SkiaRenderer : IRenderer<C64>, IRenderer
    {
        private readonly SKCanvas _skCanvas;

        private const int CHARGEN_IMAGE_CHARACTERS_PER_ROW = 16;
        private SKImage _chargenImage;

        public int MaxWidth => Vic2.PAL_PIXELS_PER_LINE_VISIBLE;
        public int MaxHeight => Vic2.PAL_LINES_VISIBLE;

        public C64SkiaRenderer(SKCanvas skCanvas)
        {
            _skCanvas = skCanvas;
        }

        public void Init(GRContext grContext, SKCanvas skCanvas)
        {
            _chargenImage = GenerateChargenImage(grContext);
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

        private SKImage GenerateChargenImage(GRContext grContext)
        {
            var chargen = new Chargen();
            // TODO: ROM directory from config file
            var chargenFile = Environment.ExpandEnvironmentVariables("%USERPROFILE%/Documents/C64/VICE/C64/chargen"); 

            SKImage image = chargen.GenerateChargenImage(grContext, chargenFile, charctersPerRow: CHARGEN_IMAGE_CHARACTERS_PER_ROW);

#if DEBUG
            //var saveDir = Environment.ExpandEnvironmentVariables("%USERPROFILE%/AppData/Local/temp");;
            var saveDir = Path.GetTempPath();
            var saveFile = $"{saveDir}/c64_chargen_dump.png";
            chargen.DumpChargenFileToImageFile(image, saveFile);
#endif
            return image;
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

            // TODO: Create pre-initialized SKPaint instances for each C64 color.
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
            int pixelPosY = row * c64.CharacterHeight;
            if (adjustForBorder)
            {
                pixelPosX += c64.BorderWidth;
                pixelPosY += c64.BorderHeight;
            }

            // Draw character image from chargen ROM to a Skia surface
            // The chargen ROM has been loaded to a SKImage with 16 characters per row (each character 8 x 8 pixels).
            int romImageX = (emulatorCharacter % CHARGEN_IMAGE_CHARACTERS_PER_ROW) * 8;
            int romImageY = (emulatorCharacter / CHARGEN_IMAGE_CHARACTERS_PER_ROW) * 8;

            // TODO: Create pre-initialized SKPaint instances for each C64 color.
            SKColor foregroundColorForCharacter = C64SkiaColors.NativeToSkColorMap[ColorMaps.C64ColorMap[emulatorFgColor]];
            byte backgroundColor = c64.Mem[Vic2Addr.BACKGROUND_COLOR];

            // TODO: Create pre-initialized ColorFilter
            using (var paint = new SKPaint { Style = SKPaintStyle.Fill, ColorFilter = CreateForceSingleColorFilter(foregroundColorForCharacter, backgroundColor) })
            {
                _skCanvas.DrawImage(_chargenImage,
                    source: new SKRect(romImageX, romImageY, romImageX + 8, romImageY + 8),
                    dest:   new SKRect(pixelPosX, pixelPosY, pixelPosX + 8, pixelPosY + 8),
                    paint
                    );
            }
        }

        /// <summary>
        /// Color filter to preserve background color, but change all other colors to specified one.
        /// The image that contains the chargen characters where generated with foreground color White (and opaque background).
        /// </summary>
        /// <param name="forceSingleColor"></param>
        /// <param name="backgroundColor"></param>
        /// <returns></returns>
        private SKColorFilter CreateForceSingleColorFilter(SKColor forceSingleColor, SKColor backgroundColor)
        {
            byte[] R = new byte[256];
            byte[] G = new byte[256];
            byte[] B = new byte[256];
            byte[] A = new byte[256];

            R[backgroundColor.Red] = backgroundColor.Red;
            G[backgroundColor.Green] = backgroundColor.Green;
            B[backgroundColor.Blue] = backgroundColor.Blue;

            for (int x = 0; x <= 255; x++)
            {
                bool changeR = x != backgroundColor.Red;
                bool changeG = x != backgroundColor.Green;
                bool changeB = x != backgroundColor.Blue;
                if (changeR)
                    R[x] = forceSingleColor.Red;
                if (changeG)
                    G[x] = forceSingleColor.Green;
                if (changeB)
                    B[x] = forceSingleColor.Blue;
                A[x] = forceSingleColor.Alpha;
            }

            var colorFilter = SKColorFilter.CreateTable(A, R, G, B);

            return colorFilter;
        }
    }
}
