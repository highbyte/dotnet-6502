using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Impl.Avalonia.Input;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.NAudio.WavePlayers;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Configuration;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

public class EmulatorConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.AvaloniaConfig";

    public string DefaultEmulator { get; set; } = "C64";
    public float DefaultDrawScale { get; set; } = 2.0f;
    public float CurrentDrawScale { get; set; } = 2.0f;
    public bool UseGlobalExceptionHandler { get; set; } = true; // If set to false, the app will crash on unhandled exceptions. Can be useful for debugging to trigger the debugger where the exception occurs.
    public bool ShowErrorDialog { get; set; } = true; // If UseGlobalExceptionHandler is true, setting ShowErrorDialog to true shows a dialog on unhandled exceptions. Otherwise, exceptions are just logged.
    public bool ShowDebugTools { get; set; } = false;
    public bool LoadResourcesOverHttp { get; set; } = false;

    public WavePlayerSettingsProfile AudioSettingsProfile { get; set; } = WavePlayerSettingsProfile.Balanced;
    [JsonConverter(typeof(JsonStringEnumConverter<BrowserSampleAudioMode>))]
    public BrowserSampleAudioMode BrowserSampleAudioMode { get; set; } = BrowserSampleAudioMode.Stable;

    /// <summary>
    /// Backwards-compatible persisted setting. Prefer <see cref="BrowserSampleAudioMode"/>.
    /// </summary>
    [JsonIgnore]
    public bool UseBrowserDirectWriteSampleAudio
    {
        get => BrowserSampleAudioMode != BrowserSampleAudioMode.Stable;
        set
        {
            if (value && BrowserSampleAudioMode == BrowserSampleAudioMode.Stable)
                BrowserSampleAudioMode = BrowserSampleAudioMode.DirectWriteAuto;
        }
    }

    public MonitorConfig Monitor { get; set; } = new();

    /// <summary>
    /// Runtime-only: the localStorage key prefix used for Lua script storage (browser only).
    /// Set by the platform host at startup; not persisted.
    /// </summary>
    [JsonIgnore]
    public string LuaStorePrefix { get; set; } = string.Empty;

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

    public void Validate(SystemList systemList)
    {
        if (!systemList.Systems.Contains(DefaultEmulator))
            throw new DotNet6502Exception($"Setting {nameof(DefaultEmulator)} value {DefaultEmulator} is not supported. Valid values are: {string.Join(',', systemList.Systems)}");
        Monitor.Validate();
    }

    public IConfigurationSection GetConfigurationSection(IConfiguration config)
    {
        return config.GetSection(ConfigSectionName);
    }

    public void WriteToConfiguration(IConfiguration config)
    {
        var configSection = GetConfigurationSection(config);
        configSection["DefaultEmulator"] = DefaultEmulator;
        configSection["DefaultDrawScale"] = DefaultDrawScale.ToString();
        configSection["ShowErrorDialog"] = ShowErrorDialog.ToString();
        configSection["ShowDebugTools"] = ShowDebugTools.ToString();
        configSection["AudioSettingsProfile"] = AudioSettingsProfile.ToString();
        configSection["BrowserSampleAudioMode"] = BrowserSampleAudioMode.ToString();

        var monitorSection = configSection.GetSection("Monitor");
        monitorSection["StopAfterBRKInstruction"] = Monitor.StopAfterBRKInstruction.ToString();
        monitorSection["StopAfterUnknownInstruction"] = Monitor.StopAfterUnknownInstruction.ToString();
    }

    public string GetConfigAsJson()
    {
        var json = JsonSerializer.Serialize(this, EmulatorConfigJsonContext.Default.EmulatorConfig);
        return json;
    }
}
