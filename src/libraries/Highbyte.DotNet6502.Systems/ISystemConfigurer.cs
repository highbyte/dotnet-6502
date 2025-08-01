namespace Highbyte.DotNet6502.Systems;

public interface ISystemConfigurer<TRenderContext, TInputHandlerContext, TAudioHandlerContext>
{
    public string SystemName { get; }
    public Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig);
    public Task<ISystem> BuildSystem(string configurationVariant, ISystemConfig systemConfig);
    public Task<IHostSystemConfig> GetNewHostSystemConfig();
    public Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig);
    public Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        IHostSystemConfig hostSystemConfig,
        TRenderContext renderContext,
        TInputHandlerContext inputHandlerContext,
        TAudioHandlerContext audioHandlerContext
        );
}
