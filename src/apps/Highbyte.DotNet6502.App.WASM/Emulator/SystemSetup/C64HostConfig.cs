using System.Text.Json.Serialization;
using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.Impl.AspNet.Commodore64.Input;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.App.WASM.Emulator.SystemSetup;

public class C64HostConfig : IHostSystemConfig, ICloneable
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64.WASM";

    //public const string DefaultCorsProxyURL = "https://api.allorigins.win/raw?url="; // Doesn't work reliably
    //public const string DefaultCorsProxyURL = "https://thingproxy.freeboard.io/fetch/"; // Doesn't seem to work with redirects
    public const string DefaultCorsProxyURL = "https://corsproxy.io/?url=";

    private C64SystemConfig _systemConfig;
    ISystemConfig IHostSystemConfig.SystemConfig => _systemConfig;

    public C64SystemConfig SystemConfig
    {
        get { return _systemConfig; }
        set { _systemConfig = value; }
    }

    [JsonIgnore]
    public bool AudioSupported => true;


    private bool _isDirty = false;
    [JsonIgnore]
    public bool IsDirty => _isDirty || _systemConfig.IsDirty;
    public void ClearDirty()
    {
        _isDirty = false;
        _systemConfig.ClearDirty();
    }

    public C64AspNetInputConfig InputConfig { get; set; } = new C64AspNetInputConfig();

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

    public C64HostConfig()
    {
        // Defaults
        _systemConfig = new C64SystemConfig();

        BasicAIAssistantDefaultEnabled = false;
        CodeSuggestionBackendType = CodeSuggestionBackendTypeEnum.CustomEndpoint;
    }

    public void Validate()
    {
        if (!IsValid(out List<string> validationErrors))
            throw new DotNet6502Exception($"Config errors: {string.Join(',', validationErrors)}");
    }

    public bool IsValid(out List<string> validationErrors)
    {
        validationErrors = new List<string>();

        SystemConfig.IsValid(out var systemConfigValidationErrors);
        validationErrors.AddRange(systemConfigValidationErrors);

        return validationErrors.Count == 0;
    }

    public object Clone()
    {
        var clone = (C64HostConfig)MemberwiseClone();
        clone._systemConfig = (C64SystemConfig)SystemConfig.Clone();
        clone.InputConfig = (C64AspNetInputConfig)InputConfig.Clone();
        return clone;
    }
}
