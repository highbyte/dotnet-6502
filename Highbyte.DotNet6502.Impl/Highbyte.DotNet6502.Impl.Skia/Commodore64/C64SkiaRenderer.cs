using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using SkiaSharp;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64
{
    public class C64SkiaRenderer : IRenderer<C64, SkiaRenderContext>, IRenderer
    {
        private SKCanvas _skCanvas;

        private const int CHARGEN_IMAGE_CHARACTERS_PER_ROW = 16;

        private SKImage _characterSetCurrent;

        private SKImage _characterSetROMShiftedImage;
        private SKImage _characterSetROMUnshiftedImage;

        public int MaxWidth => Vic2.PAL_PIXELS_PER_LINE_VISIBLE;
        public int MaxHeight => Vic2.PAL_LINES_VISIBLE;

        // public C64SkiaRenderer(SKCanvas skCanvas)
        // {
        //     _skCanvas = skCanvas;
        // }

        public void Init(C64 c64, SkiaRenderContext skiaRenderContext)
        {
            _skCanvas = skiaRenderContext.Canvas;
            InitCharset(c64, skiaRenderContext.GRContext);
        }

        public void Init(ISystem system, IRenderContext renderContext)
        {
            Init((C64)system, (SkiaRenderContext)renderContext);
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

        private void InitCharset(C64 c64, GRContext grContext)
        {
            // Generate and remember images of the Chargen ROM charset.
            GenerateROMChargenImages(c64, grContext);
            // Default to shifted ROM character set
            _characterSetCurrent = _characterSetROMShiftedImage;
            // Listen to event when the VIC2 charset address is changed to recreate a image for the charset
            c64.Vic2.CharsetAddressChanged += (s, e) => GenerateCurrentChargenImage(c64, grContext);
        }

        private void GenerateROMChargenImages(C64 c64, GRContext grContext)
        {
            // Get the two character sets (shifted & unshifted) from VIC2 view of memory (considering selected 16KB bank and charset start offset)

            var characterSets = c64.ROMData["chargen"];

            // Chargen ROM data contains two character sets (1024 bytes each).
            var characterSetShifted = characterSets.Take(Vic2.CHARACTERSET_SIZE).ToArray();
            var characterSetUnShifted = characterSets.Skip(Vic2.CHARACTERSET_SIZE).Take(Vic2.CHARACTERSET_SIZE).ToArray();

            var chargen = new Chargen();
            // Generate and save the images for the two Chargen ROM character sets
            _characterSetROMShiftedImage = chargen.GenerateChargenImage(grContext, characterSetShifted, charactersPerRow: CHARGEN_IMAGE_CHARACTERS_PER_ROW);
            _characterSetROMUnshiftedImage = chargen.GenerateChargenImage(grContext, characterSetUnShifted, charactersPerRow: CHARGEN_IMAGE_CHARACTERS_PER_ROW);

#if DEBUG
            chargen.DumpChargenFileToImageFile(_characterSetROMShiftedImage, $"{Path.GetTempPath()}/c64_chargen_shifted_dump.png");
            chargen.DumpChargenFileToImageFile(_characterSetROMUnshiftedImage, $"{Path.GetTempPath()}/c64_chargen_unshifted_dump.png");
#endif
        }

        // TODO: Vic2 class should generate event when VIC2 bank (in 0xdd00) or VIC2 character set offset (in 0xd018) is changed, so we can generate new character set image.
        //       Detect if the VIC2 address is a Chargen ROM shadow location (bank 0 and 2, offset 0x1000 or 0x1800), if so we don't need to generate new image, instead use pre-generated images we did on Init()
        private void GenerateCurrentChargenImage(C64 c64, GRContext grContext)
        {
            // If the current address points to a location in where the Chargen ROM character sets are located, we can use pre-rendered images for the character set.
            if (c64.Vic2.CharacterSetAddressInVIC2BankIsChargenROMUnshifted)
            {
                _characterSetCurrent = _characterSetROMUnshiftedImage;
                return;
            }
            else if (c64.Vic2.CharacterSetAddressInVIC2BankIsChargenROMShifted)
            {
                _characterSetCurrent = _characterSetROMShiftedImage;
                return;
            }
            // Pointing to a location where a custom character set is located. Create a image for it.
            var characterSet = c64.Vic2.Mem.ReadData(c64.Vic2.CharacterSetAddressInVIC2Bank, Vic2.CHARACTERSET_SIZE);
            var chargen = new Chargen();
            _characterSetCurrent = chargen.GenerateChargenImage(grContext, characterSet, charactersPerRow: CHARGEN_IMAGE_CHARACTERS_PER_ROW);
        }

        private void RenderMainScreen(C64 c64)
        {
            var emulatorMem = c64.Mem;

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
        /// <param name="character"></param>
        /// <param name="characterColor"></param>
        public void DrawEmulatorCharacterOnScreen(int col, int row, byte character, byte characterColor, C64 c64, bool adjustForBorder)
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
            int romImageX = (character % CHARGEN_IMAGE_CHARACTERS_PER_ROW) * 8;
            int romImageY = (character / CHARGEN_IMAGE_CHARACTERS_PER_ROW) * 8;

            // TODO: Create pre-initialized SKPaint instances for each C64 color.
            SKColor foregroundColorForCharacter = C64SkiaColors.NativeToSkColorMap[ColorMaps.C64ColorMap[characterColor]];

            // TODO: Create pre-initialized ColorFilter to change the (white) color the character images was drawn in, to the color defined for the VIC2 memory position (color ram)
            using (var paint = new SKPaint { Style = SKPaintStyle.StrokeAndFill, ColorFilter = CreateForceSingleColorFilter(foregroundColorForCharacter, Chargen.CharacterImageDrawColor) })
            //using (var paint = new SKPaint { Style = SKPaintStyle.Stroke })
            {
                _skCanvas.DrawImage(_characterSetCurrent,
                    source: new SKRect(romImageX, romImageY, romImageX + 8, romImageY + 8),
                    dest:   new SKRect(pixelPosX, pixelPosY, pixelPosX + 8, pixelPosY + 8),
                    paint
                    );
            }
        }

        /// <summary>
        /// Color filter change the color the original character image was drawn in to a specified color.
        /// </summary>
        /// <param name="forceSingleColor"></param>
        /// <param name="originalImageSingleColor"></param>
        /// <returns></returns>
        private SKColorFilter CreateForceSingleColorFilter(SKColor forceSingleColor, SKColor originalImageSingleColor)
        {
            byte[] R = new byte[256];
            byte[] G = new byte[256];
            byte[] B = new byte[256];
            byte[] A = new byte[256];

            R[originalImageSingleColor.Red] = forceSingleColor.Red;
            G[originalImageSingleColor.Green] = forceSingleColor.Green;
            B[originalImageSingleColor.Blue] = forceSingleColor.Blue;
            A[originalImageSingleColor.Alpha] = originalImageSingleColor.Alpha;

            var colorFilter = SKColorFilter.CreateTable(A, R, G, B);
            return colorFilter;
        }
    }
}
