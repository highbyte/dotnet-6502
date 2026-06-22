using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Highbyte.DotNet6502.Systems.Tests.DependencyInjection;

public class ServiceProviderValidationTests
{
    [Fact]
    public void ValidatedOptions_EnableBuiltInContainerValidation()
    {
        Assert.True(DotNet6502ServiceProviderOptions.Validated.ValidateOnBuild);
        Assert.True(DotNet6502ServiceProviderOptions.Validated.ValidateScopes);
    }

    [Fact]
    public void ValidatedOptions_FailWhenConstructorDependencyIsMissing()
    {
        var services = new ServiceCollection();
        services.AddTransient<RequiresMissingDependency>();

        var ex = Assert.Throws<AggregateException>(
            () => services.BuildServiceProvider(DotNet6502ServiceProviderOptions.Validated));

        Assert.Contains(nameof(IMissingDependency), ex.ToString());
    }

    [Fact]
    public void ValidatedOptions_FailWhenSingletonDependsOnScopedService()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedDependency>();
        services.AddSingleton<SingletonDependingOnScopedService>();

        var ex = Assert.Throws<AggregateException>(
            () => services.BuildServiceProvider(DotNet6502ServiceProviderOptions.Validated));

        Assert.Contains(nameof(ScopedDependency), ex.ToString());
    }

    [Theory]
    [InlineData("src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Core/App.axaml.cs")]
    [InlineData("src/apps/Highbyte.DotNet6502.App.Headless/Program.cs")]
    [InlineData("src/apps/SadConsole/Highbyte.DotNet6502.App.SadConsole/Program.cs")]
    [InlineData("src/apps/SilkNetNative/Highbyte.DotNet6502.App.SilkNetNative/Program.cs")]
    [InlineData("src/apps/Terminal/Highbyte.DotNet6502.App.Terminal/Program.cs")]
    public void MaintainedManualCompositionRoots_UseValidatedServiceProviderOptions(string relativePath)
    {
        var repoRoot = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(repoRoot.FullName, relativePath));

        Assert.Contains(
            "BuildServiceProvider(DotNet6502ServiceProviderOptions.Validated)",
            source);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "dotnet-6502.sln")))
            current = current.Parent;

        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed class RequiresMissingDependency(IMissingDependency missingDependency)
    {
        public IMissingDependency MissingDependency { get; } = missingDependency;
    }

    private interface IMissingDependency;

    private sealed class SingletonDependingOnScopedService(ScopedDependency scopedDependency)
    {
        public ScopedDependency ScopedDependency { get; } = scopedDependency;
    }

    private sealed class ScopedDependency;
}
