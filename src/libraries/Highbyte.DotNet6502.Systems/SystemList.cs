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
    public async Task<ISystem> GetSystem(string systemName, string configurationVariant = DEFAULT_CONFIGURATION_VARIANT)
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

        if (!await IsValidConfig(systemName, configurationVariant))
            throw new DotNet6502Exception($"Internal error. Configuration for system {cacheKey} is invalid.");


        var systemConfig = await GetSystemConfig(systemName, configurationVariant);
        var system = _systemConfigurers[systemName].BuildSystem(systemConfig);
        _systemsCache[cacheKey] = system;
    }

    private void CacheSystemConfig(string systemName, string configurationVariant, ISystemConfig systemConfig)
    {
        if (!Systems.Contains(systemName))
            throw new DotNet6502Exception($"System does not exist: {systemName}");

        var cacheKey = BuildSystemCacheKey(systemName, configurationVariant);

        // Clear any cached System
        if (_systemsCache.ContainsKey(cacheKey))
            _systemsCache.Remove(cacheKey);

        // Update the cached config
        _systemConfigsCache[cacheKey] = systemConfig;
    }

    public async Task<SystemRunner> BuildSystemRunner(
        string systemName,
        string configurationVariant = DEFAULT_CONFIGURATION_VARIANT)
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
        var systemConfig = await GetSystemConfig(systemName, configurationVariant);
        var hostSystemConfig = GetHostSystemConfig(systemName);
        var systemRunner = _systemConfigurers[systemName].BuildSystemRunner(system, systemConfig, hostSystemConfig, _getRenderContext(), _getInputHandlerContext(), _getAudioHandlerContext());
        systemRunner.Init();
        return systemRunner;
    }

    public async Task<ISystemConfig> GetSystemConfig(string systemName, string configurationVariant = DEFAULT_CONFIGURATION_VARIANT)
    {
        if (!Systems.Contains(systemName))
            throw new DotNet6502Exception($"System does not exist: {systemName}");

        var cacheKey = BuildSystemCacheKey(systemName, configurationVariant);
        if (!_systemConfigsCache.ContainsKey(cacheKey))
        {
            var systemConfig = await _systemConfigurers[systemName].GetNewConfig(configurationVariant);
            ChangeCurrentSystemConfig(systemName, systemConfig, configurationVariant);
        }
        return _systemConfigsCache[cacheKey];
    }

    public void ChangeCurrentSystemConfig(string systemName, ISystemConfig systemConfig, string configurationVariant = DEFAULT_CONFIGURATION_VARIANT)
    {
        // Make sure any cached version of the system is invalidated so it'll be re-recreated with new config.
        InvalidateSystemCache(systemName, configurationVariant);
        CacheSystemConfig(systemName, configurationVariant, systemConfig);
    }

    //public async Task PersistNewSystemConfig(string systemName, ISystemConfig updatedSystemConfig, string configurationVariant = DEFAULT_CONFIGURATION_VARIANT)
    //{
    //    ChangeCurrentSystemConfig(systemName, updatedSystemConfig, configurationVariant);
    //    await PersistSystemConfig(systemName);
    //}

    //public async Task PersistSystemConfig(string systemName, string configurationVariant = DEFAULT_CONFIGURATION_VARIANT)
    //{
    //    var systemConfig = await GetSystemConfig(systemName, configurationVariant);
    //    await _systemConfigurers[systemName].PersistConfig(systemConfig);
    //}

    public async Task<bool> IsValidConfig(string systemName, string configurationVariant = DEFAULT_CONFIGURATION_VARIANT)
    {
        var systemConfig = await GetSystemConfig(systemName, configurationVariant);
        bool isValid = systemConfig.IsValid(out List<string> _);
        return isValid;
    }

    public async Task<(bool, List<string> validationErrors)> IsValidConfigWithDetails(string systemName, string configurationVariant = DEFAULT_CONFIGURATION_VARIANT)
    {
        var systemConfig = await GetSystemConfig(systemName, configurationVariant);
        bool isValid = systemConfig.IsValid(out List<string> validationErrors);
        return (isValid, validationErrors);
    }

    public bool IsAudioSupported(string systemName, string configurationVariant = DEFAULT_CONFIGURATION_VARIANT)
    {
        var systemConfig = GetSystemConfig(systemName, configurationVariant).Result;
        return systemConfig.AudioSupported;
    }

    public bool IsAudioEnabled(string systemName, string configurationVariant = DEFAULT_CONFIGURATION_VARIANT)
    {
        var systemConfig = GetSystemConfig(systemName, configurationVariant).Result;
        return systemConfig.AudioEnabled;
    }
    public void SetAudioEnabled(string systemName, bool enabled, string configurationVariant = DEFAULT_CONFIGURATION_VARIANT)
    {
        var systemConfig = GetSystemConfig(systemName, configurationVariant).Result;
        systemConfig.AudioEnabled = enabled;
    }

    private string BuildSystemCacheKey(string systemName, string configurationVariant)
    {
        return $"{systemName}_{configurationVariant}";
    }

    public void InvalidateSystemCache(string systemName, string configurationVariant = DEFAULT_CONFIGURATION_VARIANT)
    {
        var cacheKey = BuildSystemCacheKey(systemName, configurationVariant);
        if (_systemsCache.ContainsKey(cacheKey))
        {
            _systemsCache.Remove(cacheKey);
        }
    }


    public IHostSystemConfig GetHostSystemConfig(string systemName)
    {
        if (!Systems.Contains(systemName))
            throw new DotNet6502Exception($"System does not exist: {systemName}");

        var cacheKey = systemName;
        if (!_hostSystemConfigsCache.ContainsKey(cacheKey))
        {
            var hostSystemConfig = _systemConfigurers[systemName].GetNewHostSystemConfig();
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
    }
}
