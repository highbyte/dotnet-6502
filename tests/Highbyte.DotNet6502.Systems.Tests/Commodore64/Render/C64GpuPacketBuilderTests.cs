using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Render.CustomPayload;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Render;

public class C64GpuPacketBuilderTests
{
    [Fact]
    public void Sprite_color_change_updates_metadata_without_reuploading_content()
    {
        var c64 = BuildC64();
        SeedSprite(c64, spriteNumber: 0, spritePointer: 0xC0, firstSpriteByte: 0xAA);

        _ = C64GpuPacketBuilder.CreateC64GpuPacket(c64, changedAllCharsetCodes: false, useFineScrollPerRasterLine: false);

        c64.Mem.Write(Vic2Addr.SPRITE_0_COLOR, 0x05);

        var packet = C64GpuPacketBuilder.CreateC64GpuPacket(c64, changedAllCharsetCodes: false, useFineScrollPerRasterLine: false);

        Assert.False(packet.SpriteContentDataIsDirty);
        Assert.Equal((uint)0x05, packet.SpriteData[0].Color);
        Assert.False(c64.Vic2.SpriteManager.Sprites[0].IsContentDirty);
    }

    [Fact]
    public void Sprite_data_change_reuploads_sprite_content()
    {
        var c64 = BuildC64();
        SeedSprite(c64, spriteNumber: 0, spritePointer: 0xC0, firstSpriteByte: 0xAA);

        _ = C64GpuPacketBuilder.CreateC64GpuPacket(c64, changedAllCharsetCodes: false, useFineScrollPerRasterLine: false);

        c64.Mem.Write(0x3000, 0x55);

        var packet = C64GpuPacketBuilder.CreateC64GpuPacket(c64, changedAllCharsetCodes: false, useFineScrollPerRasterLine: false);

        Assert.True(packet.SpriteContentDataIsDirty);
        Assert.Equal((uint)0x55, packet.SpriteContentData[0].Content);
        Assert.False(c64.Vic2.SpriteManager.Sprites[0].IsContentDirty);
    }

    private static C64 BuildC64()
    {
        return C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL"
        }, NullLoggerFactory.Instance);
    }

    private static void SeedSprite(C64 c64, int spriteNumber, byte spritePointer, byte firstSpriteByte)
    {
        c64.Mem.Write((ushort)(c64.Vic2.SpriteManager.SpritePointerStartAddress + spriteNumber), spritePointer);
        c64.Mem.Write((ushort)(spritePointer * 64), firstSpriteByte);
    }
}
