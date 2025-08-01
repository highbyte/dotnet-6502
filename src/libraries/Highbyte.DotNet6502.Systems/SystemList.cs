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
    /// Returns an instance of the specified system based on the current configuration.
    /// An exception is thrown if the system does not exist or the configuration is invalid.
    /// </summary>
    /// <param name="systemName"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<ISystem> BuildSystem(string systemName, string configurationVariant)
    {
        if (!Systems.Contains(systemName))
            throw new DotNet6502Exception($"System does not exist: {systemName}");

        bool isValid = await IsValidConfig(systemName);
        if (!isValid)
            throw new DotNet6502Exception($"Internal error. Configuration for system {systemName} variant {configurationVariant} is invalid.");

        var hostSystemConfig = await GetHostSystemConfig(systemName);
        var system = await _systemConfigurers[systemName].BuildSystem(configurationVariant, hostSystemConfig.SystemConfig);
        return system;
    }

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

        var system = await BuildSystem(systemName, configurationVariant);
        var hostSystemConfig = await GetHostSystemConfig(systemName);
        var systemRunner = await _systemConfigurers[systemName].BuildSystemRunner(system, hostSystemConfig, _getRenderContext(), _getInputHandlerContext(), _getAudioHandlerContext());
        systemRunner.Init();
        return systemRunner;
    }

    public async Task<SystemRunner> BuildSystemRunner(
        ISystem system)
    {
        if (_getRenderContext == null)
            throw new DotNet6502Exception("RenderContext has not been initialized. Call InitContext to initialize.");
        if (_getInputHandlerContext == null)
            throw new DotNet6502Exception("InputHandlerContext has not been initialized. Call InitContext to initialize.");
        if (_getAudioHandlerContext == null)
            throw new DotNet6502Exception("AudioHandlerContext has not been initialized. Call InitContext to initialize.");

        var hostSystemConfig = await GetHostSystemConfig(system.Name);
        var systemRunner = await _systemConfigurers[system.Name].BuildSystemRunner(system, hostSystemConfig, _getRenderContext(), _getInputHandlerContext(), _getAudioHandlerContext());
        systemRunner.Init();
        return systemRunner;
    }

    public async Task<List<string>> GetSystemConfigurationVariants(string systemName, IHostSystemConfig hostSystemConfig)
    {
        if (!Systems.Contains(systemName))
            throw new DotNet6502Exception($"System does not exist: {systemName}");
        return await _systemConfigurers[systemName].GetConfigurationVariants(hostSystemConfig.SystemConfig);
    }

    public async Task<bool> IsValidConfig(string systemName)
    {
        var hostSystemConfig = await GetHostSystemConfig(systemName);
        bool isValid = hostSystemConfig.IsValid(out List<string> _);
        return isValid;
    }

    public async Task<(bool, List<string> validationErrors)> IsValidConfigWithDetails(string systemName)
    {
        var hostSystemConfig = await GetHostSystemConfig(systemName);
        bool isValid = hostSystemConfig.IsValid(out List<string> validationErrors);
        return (isValid, validationErrors);
    }

    public async Task<bool> IsAudioSupported(string systemName)
    {
        var hostSystemConfig = await GetHostSystemConfig(systemName);
        return hostSystemConfig.AudioSupported;
    }

    public async Task<bool> IsAudioEnabled(string systemName)
    {
        var hostSystemConfig = await GetHostSystemConfig(systemName);
        return hostSystemConfig.SystemConfig.AudioEnabled;
    }
    public async Task SetAudioEnabled(string systemName, bool enabled)
    {
        var hostSystemConfig = await GetHostSystemConfig(systemName);
        hostSystemConfig.SystemConfig.AudioEnabled = enabled;
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
    }

    public async Task PersistHostSystemConfig(string systemName)
    {
        var hostSystemConfig = await GetHostSystemConfig(systemName);
        await _systemConfigurers[systemName].PersistHostSystemConfig(hostSystemConfig);
    }
}
