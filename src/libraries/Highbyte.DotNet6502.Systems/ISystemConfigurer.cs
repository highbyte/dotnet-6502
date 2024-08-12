namespace Highbyte.DotNet6502.Systems;

public interface ISystemConfigurer<TRenderContext, TInputHandlerContext, TAudioHandlerContext>
{
    public string SystemName { get; }
    public IHostSystemConfig GetNewHostSystemConfig();
    public List<string> GetConfigurationVariants();
    public Task<ISystemConfig> GetNewConfig(string configurationVariant);
    public Task PersistConfig(ISystemConfig systemConfig);
    public ISystem BuildSystem(ISystemConfig systemConfig);
    public SystemRunner BuildSystemRunner(
        ISystem system,
        ISystemConfig systemConfig,
        IHostSystemConfig hostSystemConfig,
        TRenderContext renderContext,
        TInputHandlerContext inputHandlerContext,
        TAudioHandlerContext audioHandlerContext
        );
}
