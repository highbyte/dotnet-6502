using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Systems.Generic.Render;

namespace Highbyte.DotNet6502.Systems.Generic.Config;

public class GenericComputerSystemConfig : ISystemConfig
{
    private bool _isDirty = false;

    public bool IsDirty => _isDirty;

    [JsonIgnore]
    public Type? RenderProviderType { get; private set; }

    /// <summary>
    /// Serializable version of RenderProviderType as assembly qualified name
    /// </summary>
    [JsonPropertyName("RenderProviderType")]
    public string? RenderProviderTypeName
    {
        get => RenderProviderType?.AssemblyQualifiedName;
        set => SetRenderProviderType(value != null ? Type.GetType(value) : null);
    }

    [JsonIgnore]
    public Type? RenderTargetType { get; private set; }

    /// <summary>
    /// Serializable version of RenderTargetType as assembly qualified name
    /// </summary>
    [JsonPropertyName("RenderTargetType")]
    public string? RenderTargetTypeTypeName
    {
        get => RenderTargetType?.AssemblyQualifiedName;
        set => SetRenderTargetType(value != null ? Type.GetType(value) : null);
    }

    public bool AudioEnabled { get; set; }

    public Dictionary<string, string?> ExamplePrograms
    {
        get
        {
            if (_examplePrograms.Count == 0)
            {
                _examplePrograms.Add("None", null);
            }
            else if (_examplePrograms.Count > 1 && _examplePrograms.ContainsKey("None"))
            {
                _examplePrograms.Remove("None");
            }
            return _examplePrograms;
        }
        set  // Changed from 'set' with private access to public
        {
            _examplePrograms = value ?? new Dictionary<string, string?>();
            _isDirty = true;
        }
    }

    private Dictionary<string, string?> _examplePrograms { get; set; } = new();

    public void ClearDirty()
    {
        AudioEnabled = false;
        _isDirty = false;
    }
    public List<Type> GetSupportedRenderProviderTypes()
    {
        var supportedRenderProviders = new List<Type>()
        {
            typeof(GenericVideoCommandStream)
        };
        return supportedRenderProviders;
    }
    public void SetRenderProviderType(Type renderProviderType)
    {
        var supportedRenderProviders = GetSupportedRenderProviderTypes();
        if (!supportedRenderProviders.Contains(renderProviderType))
            throw new DotNet6502Exception($"RenderProvider type {renderProviderType.FullName} is not supported.");
        RenderProviderType = renderProviderType;
    }

    public void SetRenderTargetType(Type renderTargetType)
    {
        RenderTargetType = renderTargetType;

        _isDirty = true;
    }

    public GenericComputerSystemConfig()
    {
        AudioEnabled = false;
        SetRenderProviderType(GetSupportedRenderProviderTypes().First());

    }

    public object Clone()
    {
        var clone = (GenericComputerSystemConfig)this.MemberwiseClone();
        // Deep clone the dictionary to avoid shared references
        clone._examplePrograms = new Dictionary<string, string?>(_examplePrograms);
        return clone;
    }

    public void Validate()
    {
        if (!IsValid(out List<string> validationErrors))
            throw new DotNet6502Exception($"Config errors: {string.Join(',', validationErrors)}");
    }

    public bool IsValid(out List<string> validationErrors)
    {
        validationErrors = new List<string>();

        return validationErrors.Count == 0;
    }
}
