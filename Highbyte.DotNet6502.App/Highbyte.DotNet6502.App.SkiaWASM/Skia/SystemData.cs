using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.SkiaWASM.Skia
{
    public class SystemData
    {
        public ISystem? System { get; set; }
        public IRenderer? Renderer { get; set; }
        public IInputHandler? InputHandler { get; set; }
        public SystemUserConfig? SystemUserConfig { get; set; }
    }
}
