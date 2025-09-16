using System.Reflection;

namespace Highbyte.DotNet6502.Systems.Utils;

/// <summary>
/// Utility class for getting display names from types.
/// </summary>
public static class TypeDisplayHelper
{
    /// <summary>
    /// Gets the display name for a render source type.
    /// If the type has a DisplayNameAttribute, returns that value.
    /// Otherwise, returns the Type.Name as fallback.
    /// </summary>
    /// <param name="renderSourceType">The type that implements IRenderSource</param>
    /// <returns>A user-friendly display name for UI</returns>
    public static string GetDisplayName(Type renderSourceType)
    {
        if (renderSourceType == null)
            throw new ArgumentNullException(nameof(renderSourceType));

        var displayNameAttribute = renderSourceType.GetCustomAttribute<DisplayNameAttribute>();
        return displayNameAttribute?.DisplayName ?? renderSourceType.Name;
    }

    /// <summary>
    /// Gets the help text for a type if it has a HelpTextAttribute.
    /// Otherwise, returns an empty string.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static string GetHelpText(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));
        var helpTextAttribute = type.GetCustomAttribute<HelpTextAttribute>();
        return helpTextAttribute?.Text ?? string.Empty;
    }
}
