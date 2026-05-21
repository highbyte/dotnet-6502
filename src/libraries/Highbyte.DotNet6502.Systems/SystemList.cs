using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Rendering;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems;

public class SystemList
{
    private Func<IInputHandlerContext>? _getInputHandlerContext;

    public HashSet<string> Systems = new();
    private RenderTargetProvider? _renderTargetProvider;
    private readonly Dictionary<string, ISystemConfigurer> _systemConfigurers = new();

    private const string DEFAULT_CONFIGURATION_VARIANT = "DEFAULT";

    private readonly Dictionary<string, IHostSystemConfig> _hostSystemConfigsCache = new();
    private readonly HashSet<string> _hostSystemConfigsDirty = new();

    public SystemList()
    {
    }

    public void SetContext(
        RenderTargetProvider renderTargetProvider, // New rendering pipeline
        Func<IInputHandlerContext>? getInputHandlerContext = null
        )
    {
        _renderTargetProvider = renderTargetProvider;

        if (getInputHandlerContext != null)
        {
            if (_getInputHandlerContext != null)
                throw new DotNet6502Exception("InputHandlerContext has already been set. Call SetContext only once.");
            _getInputHandlerContext = getInputHandlerContext;
        }
    }

    public void InitInputHandlerContext()
    {
        if (_getInputHandlerContext == null)
            throw new DotNet6502Exception("InputHandlerContext has not been set. Call SetContext first.");
        if (_getInputHandlerContext().IsInitialized)
            _getInputHandlerContext().Cleanup();
        _getInputHandlerContext().Init();
    }

    public bool IsInputHandlerContextInitialized => _getInputHandlerContext != null ? _getInputHandlerContext().IsInitialized : false;

    /// <summary>
    /// Add a system to the list of available systems.
    /// Should typically be done once during startup.
    /// </summary>
    public void AddSystem(
        ISystemConfigurer systemConfigurer)
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
    public async Task<ISystem> BuildSystem(string systemName, string configurationVariant)
    {
        if (!Systems.Contains(systemName))
            throw new DotNet6502Exception($"System does not exist: {systemName}");

        bool isValid = await IsValidConfig(systemName);
        if (!isValid)
            throw new DotNet6502Exception($"Internal error. Configuration for system {systemName} variant {configurationVariant} is invalid.");

        var hostSystemConfig = await GetHostSystemConfig(systemName);
        var system = await _systemConfigurers[systemName].BuildSystem(configurationVariant, hostSystemConfig.SystemConfig);

        _hostSystemConfigsDirty.Remove(systemName);
        return system;
    }

    public async Task<SystemRunner> BuildSystemRunner(
        string systemName,
        string configurationVariant)
    {
        if (_getInputHandlerContext == null)
            throw new DotNet6502Exception("InputHandlerContext has not been initialized. Call SetContext to initialize.");

        var system = await BuildSystem(systemName, configurationVariant);
        var hostSystemConfig = await GetHostSystemConfig(systemName);
        var systemRunner = await _systemConfigurers[systemName].BuildSystemRunner(system, hostSystemConfig);
        return systemRunner;
    }

    public async Task<SystemRunner> BuildSystemRunner(
        ISystem system)
    {
        if (_getInputHandlerContext == null)
            throw new DotNet6502Exception("InputHandlerContext has not been initialized. Call SetContext to initialize.");

        var hostSystemConfig = await GetHostSystemConfig(system.Name);
        var systemRunner = await _systemConfigurers[system.Name].BuildSystemRunner(system, hostSystemConfig);
        return systemRunner;
    }

    public async Task<List<string>> GetSystemConfigurationVariants(string systemName, IHostSystemConfig hostSystemConfig)
    {
        if (!Systems.Contains(systemName))
            throw new DotNet6502Exception($"System does not exist: {systemName}");
        return await _systemConfigurers[systemName].GetConfigurationVariants(hostSystemConfig.SystemConfig);
    }

    /// <summary>
    /// Removes any system that declares no configuration variants. Such a system cannot be built
    /// or run, and would crash host UIs that present a variant picker — so it is treated as
    /// unavailable, exactly like a system with no plug-in. Call once at startup, after all systems
    /// have been added. Returns the names of the systems that were removed.
    /// </summary>
    /// <remarks>
    /// A correctly written <see cref="ISystemConfigurer"/> always returns at least one variant
    /// (a single "DEFAULT" when the system has no real variants); a system reaching this method
    /// with none is a plug-in defect.
    /// </remarks>
    public async Task<List<string>> RemoveSystemsWithNoConfigurationVariants(ILogger? logger = null)
    {
        var removed = new List<string>();
        foreach (var systemName in Systems.ToList())
        {
            List<string> variants;
            try
            {
                var hostSystemConfig = await GetHostSystemConfig(systemName);
                variants = await GetSystemConfigurationVariants(systemName, hostSystemConfig);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex,
                    "System '{System}' could not be queried for configuration variants — " +
                    "treating it as unavailable and removing it from the system list.", systemName);
                variants = new List<string>();
            }

            if (variants.Count == 0)
            {
                logger?.LogError(
                    "System '{System}' has no configuration variants — it cannot be started and is " +
                    "removed from the system list. ISystemConfigurer.GetConfigurationVariants must " +
                    "return at least one variant.", systemName);
                Systems.Remove(systemName);
                _systemConfigurers.Remove(systemName);
                removed.Add(systemName);
            }
        }
        return removed;
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
        if (!_hostSystemConfigsDirty.Contains(systemName))
            _hostSystemConfigsDirty.Add(systemName);
    }

    public async Task<IHostSystemConfig> GetHostSystemConfig(string systemName)
    {
        if (!Systems.Contains(systemName))
            throw new DotNet6502Exception($"System does not exist: {systemName}");

        var cacheKey = systemName;
        if (!_hostSystemConfigsCache.ContainsKey(cacheKey))
        {
            var hostSystemConfig = await _systemConfigurers[systemName].GetNewHostSystemConfig();

            // Update current host config to newly created config.
            ChangeCurrentHostSystemConfig(systemName, hostSystemConfig);
        }

        return _hostSystemConfigsCache[cacheKey];
    }

    public void ChangeCurrentHostSystemConfig(string systemName, IHostSystemConfig systemHostConfig)
    {
        CacheHostSystemConfig(systemName, systemHostConfig);
    }

    public bool HasConfigChanged(string systemName)
    {
        return _hostSystemConfigsDirty.Contains(systemName);
    }

    public async Task ApplySupportedRenderTargetToSystemConfig(string systemName)
    {
        var hostSystemConfig = await GetHostSystemConfig(systemName);
        var renderTargetProvider = _renderTargetProvider
            ?? throw new DotNet6502Exception("RenderTargetProvider has not been set. Call SetContext first.");

        // Make sure the current selected render provider is one that is supported by the host app.
        var systemConfig = hostSystemConfig.SystemConfig;
        var systemRenderProviderTypes = systemConfig.GetSupportedRenderProviderTypes();
        var availableSystemRenderProviders = renderTargetProvider.GetCompatibleConcreteRenderProviderTypes(systemRenderProviderTypes ?? new List<Type>());
        if (availableSystemRenderProviders.Count == 0)
            throw new DotNet6502Exception($"No compatible render provider is available for system {systemName}. Supported render providers: {string.Join(", ", systemRenderProviderTypes?.Select(t => t.Name) ?? new List<string>())}");
        systemConfig.SetRenderProviderType(availableSystemRenderProviders.First());

        CacheHostSystemConfig(systemName, hostSystemConfig);
    }

    private void CacheHostSystemConfig(string systemName, IHostSystemConfig hostSystemConfig)
    {
        if (!Systems.Contains(systemName))
            throw new DotNet6502Exception($"System does not exist: {systemName}");

        var cacheKey = systemName;

        // Update the cached config
        _hostSystemConfigsCache[cacheKey] = hostSystemConfig;

        _hostSystemConfigsDirty.Add(systemName);
    }

    public async Task PersistHostSystemConfig(string systemName)
    {
        var hostSystemConfig = await GetHostSystemConfig(systemName);
        await _systemConfigurers[systemName].PersistHostSystemConfig(hostSystemConfig);
    }
}
