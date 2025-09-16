
namespace Highbyte.DotNet6502.Systems.Utils;

/// <summary>
/// Attribute used to provide a user-friendly display name for types.
/// This display name is used in UI combo boxes instead of the Type.Name property.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DisplayNameAttribute : Attribute
{
    /// <summary>
    /// Gets the user-friendly display name for the type.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Initializes a new instance of the DisplayNameAttribute with the specified display name.
    /// </summary>
    /// <param name="displayName">The user-friendly display name to show in UI.</param>
    public DisplayNameAttribute(string displayName)
    {
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
    }
}
