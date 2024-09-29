namespace Highbyte.DotNet6502.Systems.Generic.Config;

public class GenericComputerSystemConfig : ISystemConfig
{
    private bool _isDirty = false;

    public bool IsDirty => _isDirty;

    public bool AudioEnabled { get; set; }

    public Dictionary<string, string> ExamplePrograms { get; set; } = new();

    public void ClearDirty()
    {
        AudioEnabled = false;
        _isDirty = false;
    }

    public GenericComputerSystemConfig()
    {
        AudioEnabled = false;
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
