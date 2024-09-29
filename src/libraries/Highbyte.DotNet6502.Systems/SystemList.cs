namespace Highbyte.DotNet6502.Systems;

public class SystemList<TRenderContext, TInputHandlerContext, TAudioHandlerContext>
    where TRenderContext : IRenderContext
    where TInputHandlerContext : IInputHandlerContext
    where TAudioHandlerContext : IAudioHandlerContext
{
    private Func<TRenderContext>? _getRenderContext;
    private Func<TInputHandlerContext>? _getInputHandlerContext;
    private Func<TAudioHandlerContext>? _getAudioHandlerContext;

    public HashSet<string> Systems = new();
    private readonly Dictionary<string, ISystemConfigurer<TRenderContext, TInputHandlerContext, TAudioHandlerContext>> _systemConfigurers = new();

    private const string DEFAULT_CONFIGURATION_VARIANT = "DEFAULT";

    private readonly Dictionary<string, IHostSystemConfig> _hostSystemConfigsCache = new();
    private readonly Dictionary<string, ISystemConfig> _systemConfigsCache = new();
    private readonly Dictionary<string, ISystem> _systemsCache = new();

    public SystemList()
    {
    }

    public void SetContext(
    Func<TRenderContext>? getRenderContext = null,
    Func<TInputHandlerContext>? getInputHandlerContext = null,
    Func<TAudioHandlerContext>? getAudioHandlerContext = null)
    {
        if (getRenderContext != null)
        {
            if (_getRenderContext != null)
                throw new DotNet6502Exception("RenderContext has already been set. Call SetContext only once.");
            _getRenderContext = getRenderContext;
        }
        if (getInputHandlerContext != null)
        {
            if (_getInputHandlerContext != null)
                throw new DotNet6502Exception("InputHandlerContext has already been set. Call SetContext only once.");
            _getInputHandlerContext = getInputHandlerContext;
        }
        if (getAudioHandlerContext != null)
        {
            if (_getAudioHandlerContext != null)
                throw new DotNet6502Exception("AudioHandlerContext has already been set. Call SetContext only once.");  
            _getAudioHandlerContext = getAudioHandlerContext;
        }
    }

    public void InitRenderContext()
    {
        if (_getRenderContext == null)
            throw new DotNet6502Exception("RenderContext has not been set. Call SetContext first.");
        if (_getRenderContext().IsInitialized)
            _getRenderContext().Cleanup();
        _getRenderContext().Init();
    }
    public void InitInputHandlerContext()
    {
        if (_getInputHandlerContext == null)
            throw new DotNet6502Exception("InputHandlerContext has not been set. Call SetContext first.");
        if (_getInputHandlerContext().IsInitialized)
            _getInputHandlerContext().Cleanup();
        _getInputHandlerContext().Init();
    }
    public void InitAudioHandlerContext()
    {
        if (_getAudioHandlerContext == null)
            throw new DotNet6502Exception("AudioHandlerContext has not been set. Call SetContext first.");
        if (_getAudioHandlerContext().IsInitialized)
            _getAudioHandlerContext().Cleanup();
        _getAudioHandlerContext().Init();
    }

    public bool IsRenderContextInitialized => _getRenderContext != null ? _getRenderContext().IsInitialized : false;
    public bool IsInputHandlerContextInitialized => _getInputHandlerContext != null ? _getInputHandlerContext().IsInitialized : false;
    public bool IsAudioHandlerContextInitialized => _getAudioHandlerContext != null ? _getAudioHandlerContext().IsInitialized : false;

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
    public void AddSystem(
        ISystemConfigurer<TRenderContext, TInputHandlerContext, TAudioHandlerContext> systemConfigurer)
    {
        var systemName = systemConfigurer.SystemName;
        if (Systems.Contains(systemName))
            throw new DotNet6502Exception($"System already added: {systemName}");
        Systems.Add(systemName);

        _systemConfigurers[systemName] = systemConfigurer;
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
    public async Task<ISystem> GetSystem(string systemName, string configurationVariant)
    {
        if (!Systems.Contains(systemName))
            throw new DotNet6502Exception($"System does not exist: {systemName}");
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
    private async Task BuildAndCacheSystem(string systemName, string configurationVariant)
    {
        if (!Systems.Contains(systemName))
            throw new DotNet6502Exception($"System does not exist: {systemName}");

        var cacheKey = BuildSystemCacheKey(systemName, configurationVariant);

        if (_systemsCache.ContainsKey(cacheKey))
            throw new DotNet6502Exception($"Internal error. Configuration for system {cacheKey} is already in cache.");

        bool isValid = await IsValidConfig(systemName, configurationVariant);
        if (!isValid)
            throw new DotNet6502Exception($"Internal error. Configuration for system {cacheKey} is invalid.");

        var hostSystemConfig = await GetHostSystemConfig(systemName);
        var system = await _systemConfigurers[systemName].BuildSystem(configurationVariant, hostSystemConfig);
        _systemsCache[cacheKey] = system;
    }

    //private void CacheSystemConfig(string systemName, string configurationVariant, ISystemConfig systemConfig)
    //{
    //    if (!Systems.Contains(systemName))
    //        throw new DotNet6502Exception($"System does not exist: {systemName}");

    //    var cacheKey = BuildSystemCacheKey(systemName, configurationVariant);

    //    // Clear any cached System
    //    if (_systemsCache.ContainsKey(cacheKey))
    //        _systemsCache.Remove(cacheKey);

    //    // Update the cached config
    //    _systemConfigsCache[cacheKey] = systemConfig;
    //}

    public async Task<SystemRunner> BuildSystemRunner(
        string systemName,
        string configurationVariant)
    {
        if (_getRenderContext == null)
            throw new DotNet6502Exception("RenderContext has not been initialized. Call InitContext to initialize.");
        if (_getInputHandlerContext == null)
            throw new DotNet6502Exception("InputHandlerContext has not been initialized. Call InitContext to initialize.");
        if (_getAudioHandlerContext == null)
            throw new DotNet6502Exception("AudioHandlerContext has not been initialized. Call InitContext to initialize.");

        var cacheKey = BuildSystemCacheKey(systemName, configurationVariant);
        if (!_systemsCache.ContainsKey(cacheKey))
            await BuildAndCacheSystem(systemName, configurationVariant);

        var system = await GetSystem(systemName, configurationVariant);
        var hostSystemConfig = await GetHostSystemConfig(systemName);
        var systemRunner = await _systemConfigurers[systemName].BuildSystemRunner(system, hostSystemConfig, _getRenderContext(), _getInputHandlerContext(), _getAudioHandlerContext());
        systemRunner.Init();
        return systemRunner;
    }

    public async Task<List<string>> GetSystemConfigurationVariants(string systemName, IHostSystemConfig hostSystemConfig)
    {
        if (!Systems.Contains(systemName))
            throw new DotNet6502Exception($"System does not exist: {systemName}");
        return await _systemConfigurers[systemName].GetConfigurationVariants(hostSystemConfig);
    }

    public async Task<bool> IsValidConfig(string systemName, string configurationVariant)
    {
        var hostSystemConfig = await GetHostSystemConfig(systemName);
        // TODO: Check hostSystemConfig.IsValid() instead of hostSystemConfig.SystemConfig.IsValid()
        bool isValid = hostSystemConfig.SystemConfig.IsValid(out List<string> _);
        return isValid;
    }

    public async Task<(bool, List<string> validationErrors)> IsValidConfigWithDetails(string systemName, string configurationVariant)
    {
        var hostSystemConfig = await GetHostSystemConfig(systemName);
        // TODO: Check hostSystemConfig.IsValid() instead of hostSystemConfig.SystemConfig.IsValid()
        bool isValid = hostSystemConfig.SystemConfig.IsValid(out List<string> validationErrors);
        return (isValid, validationErrors);
    }

    public async Task<bool> IsAudioSupported(string systemName, string configurationVariant)
    {
        var hostSystemConfig = await GetHostSystemConfig(systemName);
        return hostSystemConfig.AudioSupported;
    }

    public async Task<bool> IsAudioEnabled(string systemName, string configurationVariant)
    {
        var hostSystemConfig = await GetHostSystemConfig(systemName);
        return hostSystemConfig.SystemConfig.AudioEnabled;
    }
    public async Task SetAudioEnabled(string systemName, bool enabled, string configurationVariant)
    {
        var hostSystemConfig = await GetHostSystemConfig(systemName);
        hostSystemConfig.SystemConfig.AudioEnabled = enabled;
    }

    private string BuildSystemCacheKey(string systemName, string configurationVariant)
    {
        return $"{systemName}_{configurationVariant}";
    }

    public void InvalidateSystemCache(string systemName, string configurationVariant)
    {
        var cacheKey = BuildSystemCacheKey(systemName, configurationVariant);
        if (_systemsCache.ContainsKey(cacheKey))
        {
            _systemsCache.Remove(cacheKey);
        }
    }

    public async Task<IHostSystemConfig> GetHostSystemConfig(string systemName)
    {
        if (!Systems.Contains(systemName))
            throw new DotNet6502Exception($"System does not exist: {systemName}");

        var cacheKey = systemName;
        if (!_hostSystemConfigsCache.ContainsKey(cacheKey))
        {
            var hostSystemConfig = await _systemConfigurers[systemName].GetNewHostSystemConfig();
            ChangeCurrentHostSystemConfig(systemName, hostSystemConfig);
        }
        return _hostSystemConfigsCache[cacheKey];
    }

    public void ChangeCurrentHostSystemConfig(string systemName, IHostSystemConfig systemHostConfig)
    {
        CacheHostSystemConfig(systemName, systemHostConfig);
    }

    private void CacheHostSystemConfig(string systemName, IHostSystemConfig hostSystemConfig)
    {
        if (!Systems.Contains(systemName))
            throw new DotNet6502Exception($"System does not exist: {systemName}");

        var cacheKey = systemName;

        // Update the cached config
        _hostSystemConfigsCache[cacheKey] = hostSystemConfig;

        // Make sure to apply up-to-date information from host config to all cached system configs for this system.
        var systemConfigCacheKeys = _systemConfigsCache.Keys.Where(k => k.StartsWith(systemName + "_")).ToList();
        foreach (var systemConfigCacheKey in systemConfigCacheKeys)
        {
            var systemConfig = _systemConfigsCache[systemConfigCacheKey];
        }
    }

    public async Task PersistHostSystemConfig(string systemName)
    {
        var hostSystemConfig = await GetHostSystemConfig(systemName);
        await _systemConfigurers[systemName].PersistHostSystemConfig(hostSystemConfig);
    }
}
