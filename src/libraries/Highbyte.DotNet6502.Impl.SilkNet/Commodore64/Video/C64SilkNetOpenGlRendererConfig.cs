namespace Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Video
{
    public class C64SilkNetOpenGlRendererConfig : ICloneable
    {
        public bool UseFineScrollPerRasterLine { get; set; }

        /// <summary>
        /// Path to the vertex shader file. If ShaderEmbeddedResourceType is set, then this is the path to the embedded resource, otherwise it's a file system path.
        /// </summary>
        //public string VertexShaderPath { get; set; } = "Commodore64/Video/C64shader.vert";
        public string VertexShaderPath { get; set; } = "Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Video.C64shader.vert";

        /// <summary>
        /// Path to the fragment shader file. If ShaderEmbeddedResourceType is set, then this is the path to the embedded resource, otherwise it's a file system path.
        /// </summary>
        //public string FragmentShaderPath { get; set; } = "Commodore64/Video/C64shader.frag";
        public string FragmentShaderPath { get; set; } = "Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Video.C64shader.frag";

        public Type? ShaderEmbeddedResourceType { get; set; } = typeof(C64SilkNetOpenGlRendererConfig);

        public bool UseTestShader { get; set; }


        public C64SilkNetOpenGlRendererConfig()
        {
            UseFineScrollPerRasterLine = false;
        }
        public object Clone()
        {
            var clone = (C64SilkNetOpenGlRendererConfig)MemberwiseClone();
            return clone;
        }
    }
}
