namespace Highbyte.DotNet6502.App.SkiaWASM.Skia
{
    public class SystemUserConfig
    {
        public Uri Uri { get; set; }
        public HttpClient HttpClient { get; set; }
        public Dictionary<string, object> UserSettings { get; set; } = new();
    }
}
