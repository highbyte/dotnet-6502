using Highbyte.DotNet6502.Systems.Generic.Render;

namespace Highbyte.DotNet6502.Systems.Generic.Config;

public class GenericComputerSystemConfig : ISystemConfig
{
    private bool _isDirty = false;

    public bool IsDirty => _isDirty;
    public Type RenderProviderType { get; private set; }
    public Type RenderTargetType { get; private set; }

    public bool AudioEnabled { get; set; }

    public Dictionary<string, string> ExamplePrograms { get; set; } = new();

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

    public GenericComputerSystemConfig()
    {
        AudioEnabled = false;
        SetRenderProviderType(GetSupportedRenderProviderTypes().First());
    }

    public object Clone()
    {
        var clone = (GenericComputerSystemConfig)this.MemberwiseClone();
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
