using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.App.Avalonia.Core.Config;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;

public class C64HostConfig : IHostSystemConfig, ICloneable
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64.Avalonia";
    public const string DefaultCorsProxyURL = "https://corsproxy.io/?url=";

    private C64SystemConfig _systemConfig = new();
    ISystemConfig IHostSystemConfig.SystemConfig => _systemConfig;

    public C64SystemConfig SystemConfig
    {
        get => _systemConfig;
        set => _systemConfig = value;
    }

    [JsonIgnore]
    public bool AudioSupported => true;

    public C64AvaloniaInputConfig InputConfig { get; set; } = new C64AvaloniaInputConfig();

    public string CorsProxyURL { get; set; } = DefaultCorsProxyURL;

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
