namespace Highbyte.DotNet6502.Systems;

public interface ISystemConfig : ICloneable
{
    void Validate();

    bool IsValid(out List<string> validationErrors);

    public bool AudioSupported { get; set; }
    public bool AudioEnabled { get; set; }
}
