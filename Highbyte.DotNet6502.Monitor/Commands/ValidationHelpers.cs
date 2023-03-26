using System.ComponentModel.DataAnnotations;
using System.Globalization;
using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Validation;

namespace Highbyte.DotNet6502.Monitor.Commands;

/// <summary>
/// </summary>
public static class ValidationHelpers
{
    public static int WriteValidationError(this MonitorBase monitor, ValidationResult validationResult)
    {
        monitor.WriteOutput(!string.IsNullOrEmpty(validationResult.ErrorMessage)
            ? validationResult.ErrorMessage
            : "Unknown validation message", MessageSeverity.Error);
        return (int)CommandResult.Ok;
    }
}

public class MustBe16BitHexValueValidator : IArgumentValidator
{
    public ValidationResult GetValidationResult(CommandArgument argument, ValidationContext context)
    {
        // This validator only runs if there is a value
        if (string.IsNullOrEmpty(argument.Value))
            return ValidationResult.Success;  //return new ValidationResult($"{argument.Name} cannot be empty");

        var addressString = argument.Value;
        bool validAddress = ushort.TryParse(addressString, NumberStyles.AllowHexSpecifier, null, out ushort word);
        if (!validAddress)
        {
            return new ValidationResult($"The value for {argument.Name} must be a 16-bit hex address");
        }
        return ValidationResult.Success;
    }
}

public class MustBe8BitHexValueValidator : IArgumentValidator
{
    public ValidationResult GetValidationResult(CommandArgument argument, ValidationContext context)
    {
        // This validator only runs if there is a value
        if (string.IsNullOrEmpty(argument.Value))
            return ValidationResult.Success;  //return new ValidationResult($"{argument.Name} cannot be empty");

        bool validByte = byte.TryParse(argument.Value, NumberStyles.AllowHexSpecifier, null, out byte byteValue);
        if (!validByte)
        {
            return new ValidationResult($"The value for {argument.Name} must be a 8-bit hex number");
        }
        return ValidationResult.Success;
    }
}

public class GreaterThan16bitValidator : IArgumentValidator
{
    private readonly CommandArgument _otherArgument;
    private readonly bool _ignoreUndefined;

    public GreaterThan16bitValidator(CommandArgument otherArgument, bool ignoreUndefined = true)
    {
        _otherArgument = otherArgument;
        _ignoreUndefined = ignoreUndefined;
    }

    public ValidationResult GetValidationResult(CommandArgument argument, ValidationContext context)
    {
        if (_ignoreUndefined && string.IsNullOrEmpty(argument.Value))
            return ValidationResult.Success;

        var value = ushort.Parse(argument.Value, NumberStyles.AllowHexSpecifier, null);
        var otherValue = ushort.Parse(_otherArgument.Value, NumberStyles.AllowHexSpecifier, null);
        return value > otherValue ? ValidationResult.Success : new ValidationResult($"The 16 bit value {argument.Name} ({argument.Value}) must higher than {_otherArgument.Name} ({_otherArgument.Value})");
    }
}