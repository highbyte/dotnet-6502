using System.Reflection;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.Headless.Generic;

/// <summary>
/// Generic-computer configurer for the Headless host. Everything system-agnostic comes from
/// <see cref="GenericComputerSystemConfigurerCore"/>; this only sources example program bytes
/// from the embedded <c>.prg</c> resources shipped in this assembly.
/// See <c>docs/system-configurer-consolidation.md</c>.
/// </summary>
public class GenericComputerHeadlessSetup : GenericComputerSystemConfigurerCore
{
    private readonly Assembly _examplesAssembly = Assembly.GetExecutingAssembly();
    private string? ExampleFileAssemblyName => _examplesAssembly.GetName().Name;

    public GenericComputerHeadlessSetup(ILoggerFactory loggerFactory, IConfiguration configuration)
        : base(loggerFactory, configuration,
            () => new GenericComputerHeadlessHostConfig(), GenericComputerHeadlessHostConfig.ConfigSectionName)
    {
    }

    protected override Task<byte[]?> LoadExampleProgramBytesAsync(string exampleProgramPath)
    {
        // Configured paths may be bare file names — prepend this assembly's resource namespace prefix.
        if (!exampleProgramPath.StartsWith(ExampleFileAssemblyName!))
            exampleProgramPath = $"{ExampleFileAssemblyName}.Resources.Sample6502Programs.Assembler.Generic.{exampleProgramPath}";

        using var resourceStream = _examplesAssembly.GetManifestResourceStream(exampleProgramPath);
        if (resourceStream == null)
            throw new Exception($"Cannot find file in embedded resources. Resource: {exampleProgramPath}");

        var exampleProgramBytes = new byte[resourceStream.Length];
        resourceStream.ReadExactly(exampleProgramBytes);
        return Task.FromResult<byte[]?>(exampleProgramBytes);
    }
}
