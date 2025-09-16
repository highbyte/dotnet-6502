
namespace Highbyte.DotNet6502.Systems.Utils;

/// <summary>
/// Attribute used to provide a user-friendly text description for types.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class HelpTextAttribute : Attribute
{
    /// <summary>
    /// Gets the user-friendly text for the type.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Initializes a new instance of the DisplayNameAttribute with the specified display name.
    /// </summary>
    /// <param name="text">The user-friendly display text to show in UI.</param>
    public HelpTextAttribute(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }
}
