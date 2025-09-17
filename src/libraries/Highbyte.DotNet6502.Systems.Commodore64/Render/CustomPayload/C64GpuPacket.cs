using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Systems.Rendering.Custom;

namespace Highbyte.DotNet6502.Systems.Commodore64.Render.CustomPayload;
// Note: Could C64GpuProvider and C64GpuPacket be moved to core C64 library because there is nothing Silk.NET-specific here?
public sealed record C64GpuPacket(
    Vic2Screen Vic2Screen,
    Vic2ScreenLayout VisibleMainScreenArea,
    bool ChangedAllCharsetCodes,
    Vic2.DispMode DisplayMode,
    Vic2.BitmMode BitmapMode,
    Vic2.CharMode CharacterMode,
    TextData[] TextScreenData,
    CharsetData[] CharsetData,
    BitmapData[] BitmapData,
    ScreenLineData[] ScreenLineData,
    SpriteData[] SpriteData,
    bool SpriteContentDataIsDirty,
    SpriteContentData[] SpriteContentData
) : IRenderPayload;
