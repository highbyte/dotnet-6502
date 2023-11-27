using System.CommandLine;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Highbyte.DotNet6502.Monitor.Commands;

/// <summary>
/// </summary>
public static class ValidationHelpers
{
    public static Argument<string> MustBe16BitHex(this Argument<string> argument)
    {
        argument.AddValidator(
            a =>
            {
                var validationError =
                    a.Tokens
                    .Select(t => t.Value)
                    .Where(v => !ushort.TryParse(v, NumberStyles.AllowHexSpecifier, null, out ushort _))
                    .Select(_ => $"Argument '{argument.Name}' is not a 16-bit hex value.")
                    .FirstOrDefault();
                if (validationError != null)
                    a.ErrorMessage = validationError;
            }
        );

        return argument;
    }

    public static Argument<string> MustBe8BitHex(this Argument<string> argument)
    {
        argument.AddValidator(
            a =>
            {
                var validationError =
                    a.Tokens
                    .Select(t => t.Value)
                    .Where(v => !byte.TryParse(v, NumberStyles.AllowHexSpecifier, null, out byte _))
                    .Select(_ => $"Argument '{argument.Name}' has one or more values that are not a 8-bit hex value.")
                    .FirstOrDefault();
                if (validationError != null)
                    a.ErrorMessage = validationError;
            }
        );

        return argument;
    }

    public static Argument<string[]> MustBe8BitHex(this Argument<string[]> argument)
    {
        argument.AddValidator(
            a =>
            {
                var validationError =
                    a.Tokens
                    .Select(t => t.Value)
                    .Where(v => !byte.TryParse(v, NumberStyles.AllowHexSpecifier, null, out byte _))
                    .Select(_ => $"Argument '{argument.Name}' has one or more values that are not a 8-bit hex value.")
                    .FirstOrDefault();
                if (validationError != null)
                    a.ErrorMessage = validationError;
            }
        );

        return argument;
    }


    public static Argument<string> GreaterThan16bit(this Argument<string> argument, Argument<string> otherArgument, bool ignoreUndefined = true)
    {
        argument.AddValidator(
            a =>
            {
                var valueRaw = a.Tokens[0].Value;
                if (ignoreUndefined && string.IsNullOrEmpty(valueRaw))
                    return;

                var value = ushort.Parse(valueRaw, NumberStyles.AllowHexSpecifier, null);
                var otherValue = ushort.Parse(a.GetValueForArgument(otherArgument), NumberStyles.AllowHexSpecifier, null);
                if (value <= otherValue)
                    a.ErrorMessage = $"The 16 bit value {argument.Name} ({value}) must higher than {otherArgument.Name} ({otherValue})";
            }
        );
        return argument;
    }

    public static Argument<string> MustBeIntegerFlag(this Argument<string> argument)
    {
        argument.AddValidator(
            a =>
            {
                // This validator only runs if there is a value
                if (string.IsNullOrEmpty(a.Tokens[0].Value))
                    return;

                bool validByte = byte.TryParse(a.Tokens[0].Value, NumberStyles.AllowHexSpecifier, null, out byte byteValue);
                if (!validByte || byteValue > 1)
                {
                    a.ErrorMessage = $"The value for {argument.Name} must be 0 or 1";
                }
            }
        );
        return argument;
    }
}
