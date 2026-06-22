using Microsoft.Extensions.DependencyInjection;

namespace Highbyte.DotNet6502.Systems.Plugins;

/// <summary>
/// Shared service-provider validation policy for application composition roots.
/// </summary>
public static class DotNet6502ServiceProviderOptions
{
    /// <summary>
    /// Always validate the full service graph and scoped lifetime usage when the container is built.
    /// The startup cost is deliberate: plug-in DI failures should be reported during host startup.
    /// </summary>
    public static ServiceProviderOptions Validated { get; } = new()
    {
        ValidateOnBuild = true,
        ValidateScopes = true
    };
}
