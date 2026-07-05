using System.Diagnostics.CodeAnalysis;

namespace Highbyte.DotNet6502.Systems.Configuration;

public static class ConfiguredTypeName
{
    public static string? Format(Type? type)
    {
        if (type == null)
            return null;

        var typeName = type.FullName;
        var assemblyName = type.Assembly.GetName().Name;
        if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(assemblyName))
            return type.AssemblyQualifiedName;

        return $"{typeName}, {assemblyName}";
    }

    [UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "Configured type names are persisted application types and are immediately validated by the receiving setters.")]
    public static Type? Resolve(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        var resolved = TryGetType(typeName);
        if (resolved != null)
            return resolved;

        var formattedTypeName = RemoveAssemblyMetadata(typeName);
        return formattedTypeName == typeName ? null : TryGetType(formattedTypeName);
    }

    private static string RemoveAssemblyMetadata(string typeName)
    {
        var typeNameSeparator = typeName.IndexOf(',');
        if (typeNameSeparator < 0)
            return typeName.Trim();

        var typePart = typeName[..typeNameSeparator].Trim();
        var assemblyPart = typeName[(typeNameSeparator + 1)..].Split(',')[0].Trim();
        if (string.IsNullOrWhiteSpace(typePart) || string.IsNullOrWhiteSpace(assemblyPart))
            return typeName;

        return $"{typePart}, {assemblyPart}";
    }

    [UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "Configured type names are persisted application types and are immediately validated by the receiving setters.")]
    private static Type? TryGetType(string typeName)
    {
        try
        {
            return Type.GetType(typeName, throwOnError: false);
        }
        catch (Exception ex) when (ex is FileLoadException or FileNotFoundException or BadImageFormatException or TypeLoadException)
        {
            return null;
        }
    }
}
