namespace Highbyte.DotNet6502.Systems;

public interface ISystemConfigurer<TRenderContext, TInputHandlerContext, TAudioHandlerContext>
{
    public string SystemName { get; }
    public Task<List<string>> GetConfigurationVariants(IHostSystemConfig hostSystemConfig);
    public Task<IHostSystemConfig> GetNewHostSystemConfig();
    public Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig);
    public Task<ISystem> BuildSystem(string configurationVariant, IHostSystemConfig hostSystemConfig);
    public Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        IHostSystemConfig hostSystemConfig,
        TRenderContext renderContext,
        TInputHandlerContext inputHandlerContext,
        TAudioHandlerContext audioHandlerContext
        );
}
