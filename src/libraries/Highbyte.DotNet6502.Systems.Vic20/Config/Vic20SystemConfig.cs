using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Systems.Vic20.Render;

namespace Highbyte.DotNet6502.Systems.Vic20.Config;

public class Vic20SystemConfig : ISystemConfig
{
    private bool _isDirty = false;
    public bool IsDirty => _isDirty;

    [JsonIgnore]
    public Type? RenderProviderType { get; private set; }

    [JsonPropertyName("RenderProviderType")]
    public string? RenderProviderTypeName
    {
        get => RenderProviderType?.AssemblyQualifiedName;
        set => SetRenderProviderType(value != null ? Type.GetType(value) : null);
    }

    [JsonIgnore]
    public Type? RenderTargetType { get; private set; }

    [JsonPropertyName("RenderTargetType")]
    public string? RenderTargetTypeName
    {
        get => RenderTargetType?.AssemblyQualifiedName;
        set => SetRenderTargetType(value != null ? Type.GetType(value) : null);
    }

    public bool AudioEnabled { get; set; } = false;

    [JsonIgnore]
    public Type? AudioProviderType => null;

    [JsonIgnore]
    public Type? AudioTargetType => null;

    public List<Type> GetSupportedRenderProviderTypes() =>
        new() { typeof(Vic20VideoCommandStream) };

    public List<Type> GetSupportedAudioProviderTypes() => new();

    public void SetRenderProviderType(Type? renderProviderType)
    {
        if (renderProviderType == null)
        {
            RenderProviderType = null;
            return;
        }
        if (!GetSupportedRenderProviderTypes().Contains(renderProviderType))
            throw new DotNet6502Exception($"Unsupported render provider: {renderProviderType.FullName}");
        RenderProviderType = renderProviderType;
    }

    public void SetRenderTargetType(Type? renderTargetType)
    {
        RenderTargetType = renderTargetType;
        _isDirty = true;
    }

    public void SetAudioProviderType(Type? audioProviderType)
    {
        if (audioProviderType != null)
            throw new DotNet6502Exception("VIC-20 has no audio providers in this stub.");
    }

    public void SetAudioTargetType(Type? audioTargetType)
    {
        if (audioTargetType != null)
            throw new DotNet6502Exception("VIC-20 has no audio targets in this stub.");
    }

    public Vic20SystemConfig()
    {
        SetRenderProviderType(GetSupportedRenderProviderTypes().First());
    }

    public void ClearDirty() => _isDirty = false;

    public object Clone() => (Vic20SystemConfig)MemberwiseClone();

    public void Validate()
    {
        if (!IsValid(out var errors))
            throw new DotNet6502Exception($"Config errors: {string.Join(", ", errors)}");
    }

    public bool IsValid(out List<string> validationErrors)
    {
        validationErrors = new List<string>();
        return true;
    }
}
