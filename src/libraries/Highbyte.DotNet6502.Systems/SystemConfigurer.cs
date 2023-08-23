namespace Highbyte.DotNet6502.Systems;

public interface SystemConfigurer<TRenderContext, TInputHandlerContext, TAudioHandlerContext>
{
    public string SystemName { get; }
    public Task<ISystemConfig> GetNewConfig(string configurationVariant);
    public Task PersistConfig(ISystemConfig systemConfig);
    public ISystem BuildSystem(ISystemConfig systemConfig);
    public SystemRunner BuildSystemRunner(
        ISystem system,
        ISystemConfig systemConfig,
        TRenderContext renderContext,
        TInputHandlerContext inputHandlerContext,
        TAudioHandlerContext audioHandlerContext
        );
}
