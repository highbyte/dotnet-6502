using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Impl.Avalonia.Input;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.NAudio.WavePlayers;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Configuration;
using Highbyte.DotNet6502.Utils;
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

    /// <summary>Include current runtime settings ("config") when saving an emulator-state snapshot.</summary>
    public bool IncludeConfigInSnapshot { get; set; } = true;

    /// <summary>Apply any settings ("config") embedded in a snapshot when loading it (opt-in).</summary>
    public bool RestoreConfigOnLoad { get; set; } = true;

    /// <summary>
    /// Optional desktop snapshot directory override. When empty, <see cref="DefaultSnapshotDirectory"/> is used.
    /// Ignored in browser, where snapshots are uploaded/downloaded through browser file APIs.
    /// </summary>
    public string SnapshotDirectory { get; set; } = string.Empty;

    [JsonIgnore]
    public static string DefaultSnapshotDirectory => AppStoragePaths.GetSnapshotsDirectory();

    [JsonIgnore]
    public string EffectiveSnapshotDirectory =>
        string.IsNullOrWhiteSpace(SnapshotDirectory) && !OperatingSystem.IsBrowser()
            ? DefaultSnapshotDirectory
            : SnapshotDirectory;

    public string ResolvedSnapshotDirectory()
        => ResolveDirectory(EffectiveSnapshotDirectory);

    private static string ResolveDirectory(string directory)
    {
        var expandedDirectory = PathHelper.ExpandOSEnvironmentVariables(directory);

        if (string.IsNullOrEmpty(expandedDirectory) || Path.IsPathRooted(expandedDirectory))
            return expandedDirectory;

        var cwdPath = Path.GetFullPath(expandedDirectory);
        return Directory.Exists(cwdPath) ? cwdPath : Path.GetFullPath(expandedDirectory, AppContext.BaseDirectory);
    }

    /// <summary>
    /// CORS proxy prefix used to route cross-origin HTTP fetches when running in the browser
    /// (WebAssembly). General browser setting shared by all systems and by URL-driven startup
    /// (<c>loadPrgUrl</c> / <c>loadD64Url</c> / <c>loadCrtUrl</c> / <c>basicUrl</c> / <c>scriptUrl</c>). Defaults to
    /// <see cref="BrowserServiceDefaults.DefaultCorsProxyUrl"/>; ignored on desktop (no proxy). See
    /// <see cref="GetCorsProxyUrl"/>.
    /// </summary>
    public string CorsProxyUrl { get; set; } = BrowserServiceDefaults.DefaultCorsProxyUrl;

    /// <summary>
    /// Enables the read-through cache used by C64 auto-downloaded <c>.d64</c>/<c>.prg</c>
    /// content. Desktop uses a file-backed cache; Browser uses IndexedDB when available.
    /// </summary>
    public bool DownloadCacheEnabled { get; set; } = true;

    /// <summary>
    /// Enables the automatic startup check for a newer released version (brew/scoop installs only).
    /// Also suppressed by the <c>CI</c> env var or <c>DOTNET6502_NO_UPDATE_CHECK</c>. The explicit
    /// "Check now" button in the About dialog ignores this and always checks.
    /// </summary>
    public bool UpdateCheckEnabled { get; set; } = true;

    /// <summary>
    /// The effective CORS proxy URL to use: the configured <see cref="CorsProxyUrl"/> (falling back
    /// to <see cref="BrowserServiceDefaults.DefaultCorsProxyUrl"/> when blank) in the browser, or
    /// <see langword="null"/> on desktop where cross-origin fetches are unrestricted.
    /// </summary>
    public string? GetCorsProxyUrl()
    {
        if (!OperatingSystem.IsBrowser())
            return null;
        return string.IsNullOrEmpty(CorsProxyUrl) ? BrowserServiceDefaults.DefaultCorsProxyUrl : CorsProxyUrl;
    }

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

    /// <summary>
    /// Runtime-only: the app's own base URL (origin + path, trailing slash) in the browser, used as
    /// the prefix for generated shareable startup links. Set by the browser host at startup; null on
    /// desktop where sharing links is not offered. Not persisted. See <see cref="GetShareBaseUrl"/>.
    /// </summary>
    [JsonIgnore]
    public string? ShareBaseUrl { get; set; }

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

    public string GetUserSettingsJson()
    {
        var settings = new EmulatorConfigUserSettings
        {
            DefaultEmulator = DefaultEmulator,
            DefaultDrawScale = DefaultDrawScale,
            ShowErrorDialog = ShowErrorDialog,
            ShowDebugTools = ShowDebugTools,
            IncludeConfigInSnapshot = IncludeConfigInSnapshot,
            RestoreConfigOnLoad = RestoreConfigOnLoad,
            SnapshotDirectory = OperatingSystem.IsBrowser() ? null : SnapshotDirectory,
            CorsProxyUrl = CorsProxyUrl,
            DownloadCacheEnabled = DownloadCacheEnabled,
            UpdateCheckEnabled = UpdateCheckEnabled,
            AudioSettingsProfile = AudioSettingsProfile,
            BrowserSampleAudioMode = BrowserSampleAudioMode,
            Monitor = new EmulatorMonitorUserSettings
            {
                StopAfterBRKInstruction = Monitor.StopAfterBRKInstruction,
                StopAfterUnknownInstruction = Monitor.StopAfterUnknownInstruction
            }
        };

        return JsonSerializer.Serialize(settings, EmulatorConfigJsonContext.Default.EmulatorConfigUserSettings);
    }
}

internal sealed class EmulatorConfigUserSettings
{
    public string DefaultEmulator { get; set; } = "C64";
    public float DefaultDrawScale { get; set; } = 2.0f;
    public bool ShowErrorDialog { get; set; } = true;
    public bool ShowDebugTools { get; set; }
    public bool IncludeConfigInSnapshot { get; set; } = true;
    public bool RestoreConfigOnLoad { get; set; } = true;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SnapshotDirectory { get; set; }
    public string CorsProxyUrl { get; set; } = BrowserServiceDefaults.DefaultCorsProxyUrl;
    public bool DownloadCacheEnabled { get; set; } = true;
    public bool UpdateCheckEnabled { get; set; } = true;
    [JsonConverter(typeof(JsonStringEnumConverter<WavePlayerSettingsProfile>))]
    public WavePlayerSettingsProfile AudioSettingsProfile { get; set; } = WavePlayerSettingsProfile.Balanced;
    [JsonConverter(typeof(JsonStringEnumConverter<BrowserSampleAudioMode>))]
    public BrowserSampleAudioMode BrowserSampleAudioMode { get; set; } = BrowserSampleAudioMode.Stable;
    public EmulatorMonitorUserSettings Monitor { get; set; } = new();
}

internal sealed class EmulatorMonitorUserSettings
{
    public bool StopAfterBRKInstruction { get; set; } = true;
    public bool StopAfterUnknownInstruction { get; set; } = true;
}
