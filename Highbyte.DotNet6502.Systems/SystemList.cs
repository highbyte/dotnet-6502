using System.IO.Enumeration;
using System.Linq;

namespace Highbyte.DotNet6502.Systems;

public class SystemList<TRenderContext, TInputHandlerContext>
{
    private Func<TRenderContext> _getRenderContext;
    private Func<TInputHandlerContext> _getInputHandlerContext;

    public HashSet<string> Systems = new();

    private const string DEFAULT_CONFIGURATION_VARIANT = "DEFAULT";

    private Dictionary<string, ISystemConfig> _systemConfigsCache = new();
    private Dictionary<string, ISystem> _systemsCache = new();

    private Dictionary<string, Func<ISystemConfig, ISystem>> _buildSystem = new();
    private Dictionary<string, Func<ISystem, ISystemConfig, TRenderContext, TInputHandlerContext, SystemRunner>> _buildSystemRunner = new();
    private Dictionary<string, Func<string, Task<ISystemConfig>>> _getNewSystemConfig = new();
    private Dictionary<string, Func<ISystemConfig, Task>> _persistSystemConfig = new();


    public SystemList()
    {
    }

    public void InitContext(Func<TRenderContext> getRenderContext, Func<TInputHandlerContext> getInputHandlerContext)
    {
        _getRenderContext = getRenderContext;
        _getInputHandlerContext = getInputHandlerContext;
    }

    /// <summary>
    /// Add a system to the list of available systems.
    /// Should typically be done once during startup.
    /// </summary>
    /// <param name="systemName">Name of the system</param>
    /// <param name="buildSystem">A callback method to build a new instance of a system with specified configuration object</param>
    /// <param name="buildSystemRunner">A callback method to build a new instance of a system runner for a system and specified renderer and input handler/param>
    /// <param name="getNewSystemConfig">A callback method to get new default configuration of the system with specified configuration variant</param>
    /// <param name="persistSystemConfig">A callback method to persist a configuration object for the system</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task AddSystem(
        string systemName,
        Func<ISystemConfig, ISystem> buildSystem,
        Func<ISystem, ISystemConfig, TRenderContext, TInputHandlerContext, SystemRunner> buildSystemRunner,
        Func<string, Task<ISystemConfig>> getNewSystemConfig,
        Func<ISystemConfig, Task> persistSystemConfig
        )
    {
        if (Systems.Contains(systemName))
            throw new Exception($"System already added: {systemName}");
        Systems.Add(systemName);

        _buildSystem[systemName] = buildSystem;
        _buildSystemRunner[systemName] = buildSystemRunner;
        _getNewSystemConfig[systemName] = getNewSystemConfig;
        _persistSystemConfig[systemName] = persistSystemConfig;
    }

    /// <summary>
    /// Returns an instance of the specified system based from cache if it exists.
    /// If the system does not exist in the cache, a new instance will be built based on the current configuration.
    /// An exception is thrown if the system does not exist in the cache and the current configuration is invalid.
    /// 
    /// </summary>
    /// <param name="systemName"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<ISystem> GetSystem(string systemName, string configurationVariant = DEFAULT_CONFIGURATION_VARIANT)
    {
        if (!Systems.Contains(systemName))
            throw new Exception($"System does not exist: {systemName}");
        var cacheKey = BuildSystemCacheKey(systemName, configurationVariant);
        if (!_systemsCache.ContainsKey(cacheKey))
            await BuildAndCacheSystem(systemName, configurationVariant);
        return _systemsCache[cacheKey];
    }

    /// <summary>
    /// Builds and caches the specified system.
    /// Requires that the configuration for the system is valid, otherwise it will throw an exception.
    /// </summary>
    /// <param name="systemName"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    private async Task BuildAndCacheSystem(string systemName, string configuraitonVariant)
    {
        if (!Systems.Contains(systemName))
            throw new Exception($"System does not exist: {systemName}");

        var cacheKey = BuildSystemCacheKey(systemName, configuraitonVariant);

        if (!await IsValidConfig(systemName, configuraitonVariant))
            throw new Exception($"Internal error. Configuration for system {cacheKey} is invalid.");

        if (_systemsCache.ContainsKey(cacheKey))
            _systemsCache.Remove(cacheKey);

        var systemConfig = await GetCurrentSystemConfig(systemName, configuraitonVariant);
        var system = _buildSystem[systemName](systemConfig);
        _systemsCache[cacheKey] = system;
    }

    private void CacheSystemConfig(string systemName, string configuraitonVariant, ISystemConfig systemConfig)
    {
        if (!Systems.Contains(systemName))
            throw new Exception($"System does not exist: {systemName}");

        var cacheKey = BuildSystemCacheKey(systemName, configuraitonVariant);

        // Clear any cached System
        if (_systemsCache.ContainsKey(cacheKey))
            _systemsCache.Remove(cacheKey);

        // Update the cached config
        _systemConfigsCache[cacheKey] = systemConfig;
    }

    public async Task<SystemRunner> BuildSystemRunner(
        string systemName,
        string configuraitonVariant = DEFAULT_CONFIGURATION_VARIANT)
    {
        if (_getRenderContext == null)
            throw new Exception("RenderContext has not been initialized. Call InitContext to initialize.");
        if (_getInputHandlerContext == null)
            throw new Exception("InputHandlerContext has not been initialized. Call InitContext to initialize.");

        await BuildAndCacheSystem(systemName, configuraitonVariant);
        var system = await GetSystem(systemName, configuraitonVariant);
        var systemConfig = await GetCurrentSystemConfig(systemName, configuraitonVariant);
        var systemRunner = _buildSystemRunner[systemName](system, systemConfig, _getRenderContext(), _getInputHandlerContext());
        return systemRunner;
    }

    public async Task<ISystemConfig> GetCurrentSystemConfig(string systemName, string configuraitonVariant = DEFAULT_CONFIGURATION_VARIANT)
    {
        if (!Systems.Contains(systemName))
            throw new Exception($"System does not exist: {systemName}");

        var cacheKey = BuildSystemCacheKey(systemName, configuraitonVariant);
        if (!_systemConfigsCache.ContainsKey(cacheKey))
        {
            var systemConfig = await _getNewSystemConfig[systemName](configuraitonVariant);
            ChangeCurrentSystemConfig(systemName, systemConfig, configuraitonVariant);
        }
        return _systemConfigsCache[cacheKey];
    }

    public void ChangeCurrentSystemConfig(string systemName, ISystemConfig systemConfig, string configuraitonVariant = DEFAULT_CONFIGURATION_VARIANT)
    {
        CacheSystemConfig(systemName, configuraitonVariant, systemConfig);
    }

    public async Task PersistNewSystemConfig(string systemName, ISystemConfig updatedSystemConfig, string configuraitonVariant = DEFAULT_CONFIGURATION_VARIANT)
    {
        ChangeCurrentSystemConfig(systemName, updatedSystemConfig, configuraitonVariant);
        await PersistCurrentSystemConfig(systemName);
    }

    public async Task PersistCurrentSystemConfig(string systemName, string configuraitonVariant = DEFAULT_CONFIGURATION_VARIANT)
    {
        var systemConfig = await GetCurrentSystemConfig(systemName, configuraitonVariant);
        await _persistSystemConfig[systemName](systemConfig);
    }

    public async Task<bool> IsValidConfig(string systemName, string configurationVariant = DEFAULT_CONFIGURATION_VARIANT)
    {
        var systemConfig = await GetCurrentSystemConfig(systemName, configurationVariant);
        bool isValid = systemConfig.IsValid(out List<string> _);
        return isValid;
    }

    public async Task<(bool, List<string> validationErrors)> IsValidConfigWithDetails(string systemName, string configuraitonVariant = DEFAULT_CONFIGURATION_VARIANT)
    {
        var systemConfig = await GetCurrentSystemConfig(systemName, configuraitonVariant);
        bool isValid = systemConfig.IsValid(out List<string> validationErrors);
        return (isValid, validationErrors);
    }

    private string BuildSystemCacheKey(string systemName, string configuraitonVariant)
    {
        return $"{systemName}_{configuraitonVariant}";
    }
}
