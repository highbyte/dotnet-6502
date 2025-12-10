using System;
using System.Net.Http;
using Highbyte.DotNet6502.Impl.Avalonia.Input;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.NAudio.WavePlayers;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

public class EmulatorConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.AvaloniaConfig";

    public string DefaultEmulator { get; set; } = "C64";
    public float DefaultDrawScale { get; set; } = 2.0f;
    public float CurrentDrawScale { get; set; } = 2.0f;
    public bool ShowErrorDialog { get; set; } = true;
    public bool LoadResourcesOverHttp { get; set; } = false;

    public WavePlayerSettingsProfile AudioSettingsProfile { get; set; } = WavePlayerSettingsProfile.Balanced;
    public MonitorConfig Monitor { get; set; } = new();

    private Func<HttpClient>? _getAppUrlHttpClient = null;
    public EmulatorConfig()
    {
        DefaultEmulator = DefaultEmulator;
        DefaultDrawScale = DefaultDrawScale;
        CurrentDrawScale = DefaultDrawScale;
        ShowErrorDialog = true;

        // Initialize MonitorConfig or other properties as needed
        Monitor = new();
    }

    public void EnableLoadResourceOverHttp(Func<HttpClient> getAppUrlHttpClient)
    {
        LoadResourcesOverHttp = true;
        _getAppUrlHttpClient = getAppUrlHttpClient;
    }
    public HttpClient? GetAppUrlHttpClient()
    {
        if (!LoadResourcesOverHttp || _getAppUrlHttpClient == null)
            return null;
        return _getAppUrlHttpClient();
    }

    public void Validate(SystemList<AvaloniaInputHandlerContext, NAudioAudioHandlerContext> systemList)
    {
        if (!systemList.Systems.Contains(DefaultEmulator))
            throw new DotNet6502Exception($"Setting {nameof(DefaultEmulator)} value {DefaultEmulator} is not supported. Valid values are: {string.Join(',', systemList.Systems)}");
        Monitor.Validate();
    }
}
