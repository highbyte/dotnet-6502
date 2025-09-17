using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2ScreenLayouts;

namespace Highbyte.DotNet6502.Systems.Commodore64.Render.CustomPayload;
public static class C64GpuPacketBuilder
{
    public static C64GpuPacket CreateC64GpuPacket(C64 c64, bool changedAllCharsetCodes, bool useFineScrollPerRasterLine)
    {
        var _c64 = c64;
        var vic2 = _c64.Vic2;
        var vic2Mem = vic2.Vic2Mem;
        var vic2Screen = vic2.Vic2Screen;
        var vic2ScreenLayouts = vic2.ScreenLayouts;

        // Visible screen area
        var visibileLayout = vic2ScreenLayouts.GetLayout(LayoutType.Visible);
        // Clip main screen area with consideration to possible 38 column and 24 row mode
        var visibleClippedScreenArea = vic2ScreenLayouts.GetLayout(LayoutType.VisibleNormalized);
        // Main screen draw area for characters, without consideration to 38 column mode or 24 row mode.
        var visibleMainScreenArea = vic2ScreenLayouts.GetLayout(LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);

        var displayMode = vic2.DisplayMode;
        var characterMode = vic2.CharacterMode;
        var bitmapMode = vic2.BitmapMode;

        var charsetData = Array.Empty<CharsetData>();
        var textScreenData = Array.Empty<TextData>();
        var bitmapData = Array.Empty<BitmapData>();
        if (displayMode == Vic2.DispMode.Text)
        {
            // Charset dot-matrix UBO
            if (changedAllCharsetCodes)
                charsetData = BuildCharsetData(_c64, fromROM: false);
            // Text screen UBO
            textScreenData = BuildTextScreenData(_c64);
        }
        else if (displayMode == Vic2.DispMode.Bitmap)
        {
            // Bitmap dot-matrix UBO
            bitmapData = BuildBitmapData(_c64);

            // TODO: Bitmap Color UBO
        }

        var screenLineData = new ScreenLineData[_c64.Vic2.Vic2Screen.VisibleHeight];
        foreach (var c64ScreenLine in _c64.Vic2.ScreenLineIORegisterValues.Keys)
        {
            if (c64ScreenLine < visibileLayout.TopBorder.Start.Y || c64ScreenLine > visibileLayout.BottomBorder.End.Y)
                continue;
            var canvasYPos = (ushort)(c64ScreenLine - visibileLayout.TopBorder.Start.Y);
            var screenLineIORegisters = _c64.Vic2.ScreenLineIORegisterValues[c64ScreenLine];
            screenLineData[canvasYPos].BorderColorCode = screenLineIORegisters.BorderColor;
            screenLineData[canvasYPos].BackgroundColor0Code = screenLineIORegisters.BackgroundColor0;
            screenLineData[canvasYPos].BackgroundColor1Code = screenLineIORegisters.BackgroundColor1;
            screenLineData[canvasYPos].BackgroundColor2Code = screenLineIORegisters.BackgroundColor2;
            screenLineData[canvasYPos].SpriteMultiColor0 = screenLineIORegisters.SpriteMultiColor0;
            screenLineData[canvasYPos].SpriteMultiColor1 = screenLineIORegisters.SpriteMultiColor1;
            screenLineData[canvasYPos].Sprite0ColorCode = screenLineIORegisters.Sprite0Color;
            screenLineData[canvasYPos].Sprite1ColorCode = screenLineIORegisters.Sprite1Color;
            screenLineData[canvasYPos].Sprite2ColorCode = screenLineIORegisters.Sprite2Color;
            screenLineData[canvasYPos].Sprite3ColorCode = screenLineIORegisters.Sprite3Color;
            screenLineData[canvasYPos].Sprite4ColorCode = screenLineIORegisters.Sprite4Color;
            screenLineData[canvasYPos].Sprite5ColorCode = screenLineIORegisters.Sprite5Color;
            screenLineData[canvasYPos].Sprite6ColorCode = screenLineIORegisters.Sprite6Color;
            screenLineData[canvasYPos].Sprite7ColorCode = screenLineIORegisters.Sprite7Color;
            screenLineData[canvasYPos].ColMode40 = screenLineIORegisters.ColMode40 ? 1u : 0u;
            screenLineData[canvasYPos].RowMode25 = screenLineIORegisters.RowMode25 ? 1u : 0u;

            if (useFineScrollPerRasterLine)
            {
                screenLineData[canvasYPos].ScrollX = (uint)screenLineIORegisters.ScrollX;
                screenLineData[canvasYPos].ScrollY = screenLineIORegisters.ScrollY;
            }
            else
            {
                screenLineData[canvasYPos].ScrollX = (uint)vic2.GetScrollX();
                screenLineData[canvasYPos].ScrollY = vic2.GetScrollY();
            }
        }

        var spriteData = new SpriteData[_c64.Vic2.SpriteManager.NumberOfSprites];
        foreach (var sprite in _c64.Vic2.SpriteManager.Sprites)
        {
            var si = sprite.SpriteNumber;
            spriteData[si].Visible = sprite.Visible ? 1u : 0u;
            spriteData[si].X = sprite.X + visibleMainScreenArea.Screen.Start.X - _c64.Vic2.SpriteManager.ScreenOffsetX;
            spriteData[si].Y = sprite.Y + visibleMainScreenArea.Screen.Start.Y - _c64.Vic2.SpriteManager.ScreenOffsetY;
            spriteData[si].Color = sprite.Color;
            spriteData[si].DoubleWidth = sprite.DoubleWidth ? 1u : 0u;
            spriteData[si].DoubleHeight = sprite.DoubleHeight ? 1u : 0u;
            spriteData[si].PriorityOverForeground = sprite.PriorityOverForeground ? 1u : 0u;
            spriteData[si].MultiColor = sprite.Multicolor ? 1u : 0u;
        }

        var spriteContentDataIsDirty = _c64.Vic2.SpriteManager.Sprites.Any(s => s.IsDirty);
        var spriteContentData = Array.Empty<SpriteContentData>();
        if (spriteContentDataIsDirty)
        {
            // TODO: Is best approach to upload all sprite content if any sprite is dirty? To minimize the number of separate UBO updates?
            spriteContentData = new SpriteContentData[_c64.Vic2.SpriteManager.NumberOfSprites * (Vic2Sprite.DEFAULT_WIDTH / 8) * Vic2Sprite.DEFAULT_HEIGTH];
            var uboIndex = 0;
            foreach (var sprite in _c64.Vic2.SpriteManager.Sprites)
            {
                var spriteEmulatorData = sprite.Data;
                foreach (var row in sprite.Data.Rows)
                {
                    foreach (var rowByte in row.Bytes)
                    {
                        spriteContentData[uboIndex++] = new SpriteContentData()
                        {
                            Content = rowByte
                        };
                    }
                }
                if (sprite.IsDirty)
                    sprite.ClearDirty();
            }
        }

        var c64GpuPacket = new C64GpuPacket(
            Vic2Screen: vic2Screen,
            VisibleMainScreenArea: visibleMainScreenArea,
            ChangedAllCharsetCodes: changedAllCharsetCodes,
            DisplayMode: displayMode,
            BitmapMode: bitmapMode,
            CharacterMode: characterMode,
            TextScreenData: textScreenData,
            CharsetData: charsetData,
            BitmapData: bitmapData,
            ScreenLineData: screenLineData,
            SpriteData: spriteData,
            SpriteContentDataIsDirty: spriteContentDataIsDirty,
            SpriteContentData: spriteContentDataIsDirty ? spriteContentData : Array.Empty<SpriteContentData>()
        );

        return c64GpuPacket;
    }

    public static TextData[] BuildTextScreenData(C64 c64)
    {
        var vic2 = c64.Vic2;
        var vic2Mem = vic2.Vic2Mem;
        var vic2Screen = vic2.Vic2Screen;

        var videoMatrixBaseAddress = vic2.VideoMatrixBaseAddress;
        var colorAddress = Vic2Addr.COLOR_RAM_START;

        // 40 columns, 25 rows = 1024 items
        var textData = new TextData[vic2Screen.TextCols * vic2Screen.TextRows];
        for (var i = 0; i < textData.Length; i++)
        {
            textData[i].Character = vic2Mem[(ushort)(videoMatrixBaseAddress + i)];
            textData[i].Color = c64.ReadIOStorage((ushort)(colorAddress + i));
        }
        return textData;
    }

    public static CharsetData[] BuildCharsetData(C64 c64, bool fromROM)
    {
        var charsetManager = c64.Vic2.CharsetManager;
        // A character is defined by 8 bytes (1 line per byte), 256 total characters = 2048 items
        var charsetData = new CharsetData[Vic2CharsetManager.CHARACTERSET_SIZE];

        if (fromROM)
        {
            var charsets = c64.ROMData[C64SystemConfig.CHARGEN_ROM_NAME];
            for (var i = 0; i < charsetData.Length; i++)
            {
                charsetData[i].CharLine = charsets[(ushort)i];
            }
            ;
        }
        else
        {
            for (var i = 0; i < charsetData.Length; i++)
            {
                charsetData[i].CharLine = c64.Vic2.Vic2Mem[(ushort)(charsetManager.CharacterSetAddressInVIC2Bank + i)];
            }
            ;
        }

        return charsetData;
    }

    public static BitmapData[] BuildBitmapData(C64 c64)
    {
        var bitmapManager = c64.Vic2.BitmapManager;

        var vic2Mem = c64.Vic2.Vic2Mem;
        var videoMatrixBaseAddress = c64.Vic2.VideoMatrixBaseAddress;
        var colorAddress = Vic2Addr.COLOR_RAM_START;

        // 1000 (40x25) "chars", that each contains 8 bytes (lines) where each line is 8 pixels.
        const int numberOfChars = Vic2BitmapManager.BITMAP_SIZE / 8;
        var bitmapData = new BitmapData[numberOfChars];
        for (var c = 0; c < numberOfChars; c++)
        {
            var charOffset = bitmapManager.BitmapAddressInVIC2Bank + c * 8;
            for (var line = 0; line < 8; line++)
            {
                var charLine = vic2Mem[(ushort)(charOffset + line)];
                //bitmapData[charPos].Lines[lineIndex] = charLine;
                switch (line)
                {
                    case 0:
                        bitmapData[c].Line0 = charLine; break;
                    case 1:
                        bitmapData[c].Line1 = charLine; break;
                    case 2:
                        bitmapData[c].Line2 = charLine; break;
                    case 3:
                        bitmapData[c].Line3 = charLine; break;
                    case 4:
                        bitmapData[c].Line4 = charLine; break;
                    case 5:
                        bitmapData[c].Line5 = charLine; break;
                    case 6:
                        bitmapData[c].Line6 = charLine; break;
                    case 7:
                        bitmapData[c].Line7 = charLine; break;
                }
            }

            bitmapData[c].BackgroundColorCode = (uint)(vic2Mem[(ushort)(videoMatrixBaseAddress + c)] & 0b00001111);
            bitmapData[c].ForegroundColorCode = (uint)(vic2Mem[(ushort)(videoMatrixBaseAddress + c)] & 0b11110000) >> 4;
            bitmapData[c].ColorRAMColorCode = c64.ReadIOStorage((ushort)(colorAddress + c));
        }

        return bitmapData;
    }
}
