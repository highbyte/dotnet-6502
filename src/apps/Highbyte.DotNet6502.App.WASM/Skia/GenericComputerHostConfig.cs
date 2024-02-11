using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.WASM.Skia
{
    public class GenericComputerHostConfig : IHostSystemConfig, ICloneable
    {
        public object Clone()
        {
            var clone = (GenericComputerHostConfig)MemberwiseClone();
            return clone;
        }
    }
}
