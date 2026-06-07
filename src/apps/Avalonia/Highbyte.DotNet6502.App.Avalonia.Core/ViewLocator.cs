using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

public class ViewLocator : IDataTemplate
{
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "View lookup is intentionally reflection-based and the browser publish roots the relevant assemblies explicitly.")]
    [UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "View names are derived from rooted ViewModel types and resolved from explicitly rooted assemblies.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Resolved view types are application-owned controls with public parameterless constructors.")]
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);

        // Type.GetType only searches the executing assembly and mscorlib. ViewModels and Views
        // can live in plug-in assemblies (e.g. App.Avalonia.Shell.Commodore64) — scan all loaded
        // assemblies for the resolved View type.
        var type = Type.GetType(name)
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(name, throwOnError: false))
                .FirstOrDefault(t => t is not null);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
