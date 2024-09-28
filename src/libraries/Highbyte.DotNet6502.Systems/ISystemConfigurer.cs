namespace Highbyte.DotNet6502.Systems;

public interface ISystemConfigurer<TRenderContext, TInputHandlerContext, TAudioHandlerContext>
{
    public string SystemName { get; }
    public List<string> ConfigurationVariants { get; }
    public Task<IHostSystemConfig> GetNewHostSystemConfig();
    public Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig);
    public Task<ISystemConfig> GetNewConfig(string configurationVariant, IHostSystemConfig hostSystemConfig);
    public Task PersistConfig(ISystemConfig systemConfig);
    public ISystem BuildSystem(ISystemConfig systemConfig);
    public Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        ISystemConfig systemConfig,
        IHostSystemConfig hostSystemConfig,
        TRenderContext renderContext,
        TInputHandlerContext inputHandlerContext,
        TAudioHandlerContext audioHandlerContext
        );
}
