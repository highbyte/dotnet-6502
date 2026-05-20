namespace Highbyte.DotNet6502.Systems;

public interface ISystemConfigurer
{
    public string SystemName { get; }
    public Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig);
    public Task<ISystem> BuildSystem(string configurationVariant, ISystemConfig systemConfig);
    public Task<IHostSystemConfig> GetNewHostSystemConfig();
    public Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig);

    /// <summary>
    /// Builds the <see cref="SystemRunner"/> for a run of the system.
    ///
    /// Neither the host input context nor the host audio context is threaded through here anymore:
    /// the configurer assigns the system's reusable <see cref="ISystem.InputConsumer"/> (the host
    /// input state is bound separately by the host app), and audio targets are registered host-side
    /// — both mirroring the render pipeline.
    /// </summary>
    public Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        IHostSystemConfig hostSystemConfig
        );
}
