using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.WASM.Emulator;

public class EmulatorConfig
{
    public const int DEFAULT_CANVAS_WINDOW_WIDTH = 640;
    public const int DEFAULT_CANVAS_WINDOW_HEIGHT = 400;

    public required string DefaultEmulator { get; set; }
    public double DefaultDrawScale { get; set; }
    public double CurrentDrawScale { get; set; }
    public required MonitorConfig Monitor { get; set; }

    /// <summary>
    /// CORS proxy prefix used to route cross-origin HTTP fetches (system / ROM downloads). General
    /// browser setting shared by all systems, no longer per-system. Defaults to
    /// <see cref="BrowserServiceDefaults.DefaultCorsProxyUrl"/>. See <see cref="GetCorsProxyUrl"/>.
    /// </summary>
    public string CorsProxyUrl { get; set; } = BrowserServiceDefaults.DefaultCorsProxyUrl;

    /// <summary>
    /// The effective CORS proxy URL: the configured <see cref="CorsProxyUrl"/>, falling back to
    /// <see cref="BrowserServiceDefaults.DefaultCorsProxyUrl"/> when blank. (The browser app always
    /// runs in WebAssembly, so a proxy is always used.)
    /// </summary>
    public string GetCorsProxyUrl()
        => string.IsNullOrEmpty(CorsProxyUrl) ? BrowserServiceDefaults.DefaultCorsProxyUrl : CorsProxyUrl;

    public EmulatorConfig()
    {
        DefaultDrawScale = 2.0;
        CurrentDrawScale = DefaultDrawScale;
    }

    public void Validate(SystemList systemList)
    {
        if (!systemList.Systems.Contains(DefaultEmulator))
            throw new DotNet6502Exception($"Setting {nameof(DefaultEmulator)} value {DefaultEmulator} is not supported. Valid values are: {string.Join(',', systemList.Systems)}");
        Monitor.Validate();
    }
}
