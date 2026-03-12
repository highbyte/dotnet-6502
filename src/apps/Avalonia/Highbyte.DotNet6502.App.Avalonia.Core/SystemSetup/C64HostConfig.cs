using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.Impl.Avalonia.Commodore64.Input;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;

public class C64HostConfig : IHostSystemConfig, ICloneable
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64.Avalonia";

    //public const string DefaultCorsProxyURL = "https://api.allorigins.win/raw?url="; // Doesn't work reliably
    //public const string DefaultCorsProxyURL = "https://corsproxy.io/?url="; // Stopped being possible to download binary files on free tier
    //public const string DefaultCorsProxyURL = "https://proxy.corsfix.com/?url="; // Only free from localhost
    //public const string DefaultCorsProxyURL = "https://cors-anywhere.com/"; // Only works from localhost 
    public const string DefaultCorsProxyURL = "https://api.codetabs.com/v1/proxy?quest=";

    private C64SystemConfig _systemConfig = new();
    ISystemConfig IHostSystemConfig.SystemConfig => _systemConfig;

    public C64SystemConfig SystemConfig
    {
        get => _systemConfig;
        set => _systemConfig = value;
    }

    [JsonIgnore]
    public bool AudioSupported => PlatformDetection.IsRunningOnDesktop() || PlatformDetection.IsRunningInWebAssembly();

    public C64AvaloniaInputConfig InputConfig { get; set; } = new C64AvaloniaInputConfig();

    /// <summary>
    /// Cors Proxy address override.
    /// If set to null or empty, the default CORS proxy URL will be used when running in WebAssembly. When running on desktop, this setting is ignored and no CORS proxy will be used.
    /// </summary>
    /// <value></value>
    public string? CorsProxyOverrideURL { get; set; } = null;

    /// <summary>
    /// Return the current CORS proxy URL to use.
    /// If running in WebAssembly, this will return the CorsProxyOverrideURL if set, or the DefaultCorsProxyURL if CorsProxyOverrideURL is null or empty. 
    /// If not running in WebAssembly, this will return null to indicate that no CORS proxy should be used.
    /// </summary>
    /// <returns></returns> <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public string GetCorsProxyURL()
    {
        if (!PlatformDetection.IsRunningInWebAssembly())
        {
            // CORS proxy is only needed when running in WebAssembly, so return null to not use any proxy when running on desktop
            return null!;
        }
        // If running in WebAssembly, return the configured CORS proxy URL, or the default if not set
        return string.IsNullOrEmpty(CorsProxyOverrideURL) ? DefaultCorsProxyURL : CorsProxyOverrideURL;
    }

    private bool _basicAIAssistantDefaultEnabled;
    [JsonIgnore]
    public bool BasicAIAssistantDefaultEnabled
    {
        get => _basicAIAssistantDefaultEnabled;
        set
        {
            _basicAIAssistantDefaultEnabled = value;
            _isDirty = true;
        }
    }
    private CodeSuggestionBackendTypeEnum _codeSuggestionBackendType;
    public CodeSuggestionBackendTypeEnum CodeSuggestionBackendType
    {
        get => _codeSuggestionBackendType;
        set
        {
            _codeSuggestionBackendType = value;
            _isDirty = true;
        }
    }

    private bool _isDirty = false;
    [JsonIgnore]
    public bool IsDirty => _isDirty || _systemConfig.IsDirty;
    public void ClearDirty()
    {
        _isDirty = false;
        _systemConfig.ClearDirty();
    }

    public void Validate()
    {
        if (!IsValid(out var validationErrors))
        {
            throw new Exception($"Invalid configuration: {string.Join(", ", validationErrors)}");
        }
    }

    public bool IsValid(out List<string> validationErrors)
    {
        validationErrors = new List<string>();

        SystemConfig.IsValid(out var systemConfigValidationErrors);
        validationErrors.AddRange(systemConfigValidationErrors);

        return validationErrors.Count == 0;
    }

    public C64HostConfig()
    {
        _systemConfig = new C64SystemConfig();

        BasicAIAssistantDefaultEnabled = false;
        CodeSuggestionBackendType = CodeSuggestionBackendTypeEnum.CustomEndpoint;
    }

    public object Clone()
    {
        var clone = (C64HostConfig)this.MemberwiseClone();
        clone._systemConfig = (C64SystemConfig)_systemConfig.Clone();
        clone.InputConfig = (C64AvaloniaInputConfig)InputConfig.Clone();
        return clone;
    }
}
