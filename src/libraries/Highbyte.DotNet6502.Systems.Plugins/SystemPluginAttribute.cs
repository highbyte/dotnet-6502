using System.Diagnostics.CodeAnalysis;

namespace Highbyte.DotNet6502.Systems.Plugins;

/// <summary>
/// Marks an assembly as containing a system plugin. The discovery scan reads these
/// attributes from loaded assemblies to find plugin types without scanning every type.
/// Multiple attributes per assembly are allowed (e.g. an engine plugin and a shell plugin
/// could ship in the same assembly).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class SystemPluginAttribute : Attribute
{
    /// <summary>
    /// The plugin type. <see cref="SystemPluginDiscovery"/> instantiates it via
    /// <see cref="Activator.CreateInstance(Type)"/>, so the trimmer / AOT compiler must be told to
    /// preserve its public parameterless constructor — otherwise a published (trimmed/AOT) build
    /// throws <see cref="MissingMethodException"/> ("Arg_NoDefCTor").
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type PluginType { get; }

    public SystemPluginAttribute(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        Type pluginType)
    {
        PluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
    }
}
