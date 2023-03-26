namespace Highbyte.DotNet6502.Systems;

public interface ISystemConfig
{
    void Validate();

    bool IsValid(out List<string> validationErrors);
}
