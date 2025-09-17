using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.Custom;
using Highbyte.DotNet6502.Systems.Utils;

namespace Highbyte.DotNet6502.Systems.Commodore64.Render.CustomPayload;

[DisplayName("Custom GPU packet")]
[HelpText("Provides GPU payload data for each frame.\nCan be used to build custom renderers that uses a OpenGL shader to perform all rendering.")]
public sealed class C64GpuProvider : IRenderProvider, IPayloadProvider<C64GpuPacket>
{
    public string Name => "C64GpuProvider";

    private readonly bool _useFineScrollPerRasterLine;
    private bool _changedAllCharsetCodes;
    private readonly C64 _c64;

    private C64GpuPacket? _latest;
    public event EventHandler<C64GpuPacket>? PayloadProduced;

    public C64GpuProvider(C64 c64, bool useFineScrollPerRasterLine)
    {
        _c64 = c64;
        _useFineScrollPerRasterLine = useFineScrollPerRasterLine;
        _c64.Vic2.CharsetManager.CharsetAddressChanged += (s, e) => CharsetChangedHandler(_c64, e);
    }

    public void OnAfterInstruction()
    {
    }

    public void OnEndFrame()
    {
        var c64GpuPacket = C64GpuPacketBuilder.CreateC64GpuPacket(_c64, _changedAllCharsetCodes, _useFineScrollPerRasterLine);
        _latest = c64GpuPacket;
        PayloadProduced?.Invoke(this, c64GpuPacket);
    }

    public bool TryGetLatest(out C64GpuPacket? payload)
    {
        payload = Interlocked.Exchange(ref _latest, null);
        return payload is not null;
    }

    private void CharsetChangedHandler(C64 c64, Vic2CharsetManager.CharsetAddressChangedEventArgs e)
    {
        if (e.ChangeType == Vic2CharsetManager.CharsetAddressChangedEventArgs.CharsetChangeType.CharacterSetBaseAddress)
        {
            _changedAllCharsetCodes = true;
        }
        else if (e.ChangeType == Vic2CharsetManager.CharsetAddressChangedEventArgs.CharsetChangeType.CharacterSetCharacter && e.CharCode.HasValue)
        {
            // Updating individual characters in the UBO array probably take longer time than just updating the entire array.
            _changedAllCharsetCodes = true;
            //if (!_changedCharsetCodes.Contains(e.CharCode.Value))
            //    _changedCharsetCodes.Add(e.CharCode.Value);
        }
    }
}
