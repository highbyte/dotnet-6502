using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.AI.CodingAssistant.Inference.OpenAI;
using Highbyte.DotNet6502.App.Avalonia.Core.Input;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Models;
using Highbyte.DotNet6502.Systems.Commodore64.Utils.BasicAssistant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using static Highbyte.DotNet6502.AI.CodingAssistant.CustomAIEndpointCodeSuggestion;

namespace Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;

public class C64Setup : ISystemConfigurer<AvaloniaInputHandlerContext, NullAudioHandlerContext>
{
    public string SystemName => C64.SystemName;

    private readonly Func<string, Task<string>>? _getCustomConfigString = null;
    private readonly Func<string, string, Task>? _saveCustomConfigString = null;

    public Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig) => Task.FromResult(s_systemVariants);

    private static readonly List<string> s_systemVariants = C64ModelInventory.C64Models.Keys.ToList();

    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<C64Setup> _logger;
    private readonly IConfiguration _configuration;

    private const string DEFAULT_CODE_ASSISTANT_CUSTOMENDPOINT = "https://highbyte-dotnet6502-codecompletion.azurewebsites.net/";
    // Note: For now, use a public visible key as default just to prevent at least some random users to access the endpoint...
    private const string DEFAULT_CODE_ASSISTANT_CUSTOMENDPOINT_APIKEY = "9fe8f8161c1d43251a46bb576336a1a25d7ab607cb5a1b4b960c0949d87bced7";

    public C64Setup(
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        Func<string, Task<string>>? getCustomConfigString = null,
        Func<string, string, Task>? saveCustomConfigString = null)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<C64Setup>();
        _configuration = configuration;
        _getCustomConfigString = getCustomConfigString;
        _saveCustomConfigString = saveCustomConfigString;
    }

    public async Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        if (_getCustomConfigString == null)
        {
            return await GetNewHostSystemConfigFromAppSettings();
        }

        _logger.LogInformation("Loading C64HostConfig from custom JSON source.");
        // Get config from supplied raw JSON string
        C64HostConfig? hostConfig = null;
        try
        {
            // Get config from supplied raw JSON string
            string jsonString = await _getCustomConfigString(C64HostConfig.ConfigSectionName);
            if (!string.IsNullOrEmpty(jsonString))
            {
                // Deserialize using a JsonSerializerContext configured for source generation (to be compatible with AOT compilation)
                var deserializedConfig = JsonSerializer.Deserialize(
                    jsonString,
                    HostConfigJsonContext.Default.C64HostConfig);
                if (deserializedConfig != null)
                {
                    _logger.LogInformation("Successfully deserialized C64HostConfig from JSON.");
                    hostConfig = deserializedConfig;

                    // Note: Because ROMDirectory should never be used when running in Browser, make sure it is empty (regardless if config from local storage has it set).
                    hostConfig.SystemConfig.ROMDirectory = "";
               }
                else
                {
                    _logger.LogWarning("Deserialized C64HostConfig is null, using default config.");
                }

            }
        }
        catch (Exception ex)
        {
            // Log error and continue with default config
            _logger.LogWarning(ex, "Failed to load config from JSON, using default config");
        }

        if (hostConfig == null)
        {
            _logger.LogWarning("No JSON config available, using default config.");
            hostConfig = new C64HostConfig();
            hostConfig.SystemConfig.ROMDirectory = "";
            hostConfig.SystemConfig.AudioEnabled = false;
        }

        return hostConfig;
    }

    public Task<IHostSystemConfig> GetNewHostSystemConfigFromAppSettings()
    {
        var c64HostConfig = new C64HostConfig();
        _configuration.GetSection($"{C64HostConfig.ConfigSectionName}").Bind(c64HostConfig);

        // TODO: Code suggestion AI backend type should not be set in system specific config.
        //       For now workaround by reading from a common setting.
        c64HostConfig.CodeSuggestionBackendType = Enum.Parse<CodeSuggestionBackendTypeEnum>(_configuration["CodingAssistant:CodingAssistantType"] ?? "None");

        return Task.FromResult<IHostSystemConfig>(c64HostConfig);
    }

    public async Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        if (_saveCustomConfigString == null)
        {
            _logger.LogWarning("No method for saving custom config JSON supplied, so not saving C64HostConfig.");
            return;
        }
        var json = JsonSerializer.Serialize(hostSystemConfig, HostConfigJsonContext.Default.C64HostConfig);
        await _saveCustomConfigString(C64HostConfig.ConfigSectionName, json);
    }

    public Task<ISystem> BuildSystem(string configurationVariant, ISystemConfig systemConfig)
    {
        var c64SystemConfig = (C64SystemConfig)systemConfig;
        var c64Config = new C64Config
        {
            C64Model = configurationVariant,
            Vic2Model = C64ModelInventory.C64Models[configurationVariant].Vic2Models.First().Name,
            AudioEnabled = c64SystemConfig.AudioEnabled,
            KeyboardJoystickEnabled = c64SystemConfig.KeyboardJoystickEnabled,
            KeyboardJoystick = c64SystemConfig.KeyboardJoystick,
            ROMs = c64SystemConfig.ROMs,
            ROMDirectory = c64SystemConfig.ROMDirectory,
            RenderProviderType = c64SystemConfig.RenderProviderType,
        };

        var c64 = C64.BuildC64(c64Config, _loggerFactory);
        return Task.FromResult<ISystem>(c64);
    }

    public async Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        IHostSystemConfig hostSystemConfig,
        AvaloniaInputHandlerContext inputHandlerContext,
        NullAudioHandlerContext audioHandlerContext
        )
    {
        var c64HostConfig = (C64HostConfig)hostSystemConfig;
        var c64 = (C64)system;


        ICodeSuggestion codeSuggestion;
        C64BasicCodingAssistant c64BasicCodingAssistant;
        if (_getCustomConfigString == null)
        {
            codeSuggestion = CodeSuggestionConfigurator.CreateCodeSuggestion(c64HostConfig.CodeSuggestionBackendType, _configuration, C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION, C64BasicCodingAssistant.CODE_COMPLETION_ADDITIONAL_SYSTEM_INSTRUCTION, defaultToNoneIdConfigError: true);
            c64BasicCodingAssistant = new C64BasicCodingAssistant(c64, codeSuggestion, _loggerFactory);
        }
        else
        {
            codeSuggestion = await GetCodeSuggestionImplementation(c64HostConfig);
            c64BasicCodingAssistant = new C64BasicCodingAssistant(c64, codeSuggestion, _loggerFactory);
        }

        var inputHandler = new AvaloniaC64InputHandler(c64, inputHandlerContext, _loggerFactory, c64HostConfig.InputConfig, c64BasicCodingAssistant, c64HostConfig.BasicAIAssistantDefaultEnabled);
        var audioHandler = new NullAudioHandler(c64);

        return new SystemRunner(c64, inputHandler, audioHandler);
    }

    private async Task<ICodeSuggestion> GetCodeSuggestionImplementation(C64HostConfig c64HostConfig, bool defaultToNoneIdConfigError = true)
    {
        ICodeSuggestion codeSuggestion;
        try
        {
            if (c64HostConfig.CodeSuggestionBackendType == CodeSuggestionBackendTypeEnum.OpenAI)
            {
                var openAIApiConfig = await GetOpenAIConfig(_getCustomConfigString);
                codeSuggestion = OpenAICodeSuggestion.CreateOpenAICodeSuggestion(openAIApiConfig, C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION, C64BasicCodingAssistant.CODE_COMPLETION_ADDITIONAL_SYSTEM_INSTRUCTION);
            }
            else if (c64HostConfig.CodeSuggestionBackendType == CodeSuggestionBackendTypeEnum.OpenAISelfHostedCodeLlama)
            {
                var openAIApiConfig = await GetOpenAISelfHostedCodeLlamaConfig(_getCustomConfigString);
                codeSuggestion = OpenAICodeSuggestion.CreateOpenAICodeSuggestionForCodeLlama(openAIApiConfig, C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION, C64BasicCodingAssistant.CODE_COMPLETION_ADDITIONAL_SYSTEM_INSTRUCTION);
            }
            else if (c64HostConfig.CodeSuggestionBackendType == CodeSuggestionBackendTypeEnum.CustomEndpoint)
            {
                var customAIEndpointConfig = await GetCustomAIEndpointConfig(_getCustomConfigString);
                codeSuggestion = new CustomAIEndpointCodeSuggestion(customAIEndpointConfig, C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION);
            }
            else if (c64HostConfig.CodeSuggestionBackendType == CodeSuggestionBackendTypeEnum.None)
            {
                codeSuggestion = new NoCodeSuggestion();
            }
            else
            {
                throw new NotImplementedException($"CodeSuggestionBackendType '{c64HostConfig.CodeSuggestionBackendType}' is not implemented.");
            }
        }
        catch (Exception ex)
        {
            if (defaultToNoneIdConfigError)
                codeSuggestion = new NoCodeSuggestion();
            else
                throw;
        }
        return codeSuggestion;

    }

    public static async Task<ApiConfig> GetOpenAIConfig(Func<string, Task<string>> getCustomConfigJson)
    {
        string apiKey = await getCustomConfigJson($"{ApiConfig.CONFIG_SECTION}:ApiKey");

        var deploymentName = await getCustomConfigJson($"{ApiConfig.CONFIG_SECTION}:DeploymentName");
        if (string.IsNullOrEmpty(deploymentName))
            deploymentName = "gpt-4o";  // Default to a OpenAI model that works well

        // For future use: Endpoint can be set if OpenAI is accessed via Azure endpoint.
        //var endpoint = await localStorageService.GetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION}:Endpoint");
        //Uri.TryCreate(endpoint, UriKind.Absolute, out var endPointUri);

        var apiConfig = new ApiConfig()
        {
            ApiKey = apiKey,    // Api key for OpenAI (required), Azure OpenAI (required), or SelfHosted (optional).
            DeploymentName = deploymentName, // AI model name
            //Endpoint = endPointUri,     // Used if using Azure OpenAI
            SelfHosted = false,
        };
        return apiConfig;
    }

    public static async Task<ApiConfig> GetOpenAISelfHostedCodeLlamaConfig(Func<string, Task<string>> getCustomConfigJson)
    {
        var apiKey = await getCustomConfigJson($"{ApiConfig.CONFIG_SECTION_SELF_HOSTED}:ApiKey");
        if (apiKey == string.Empty)
            apiKey = null;
        var deploymentName = await getCustomConfigJson($"{ApiConfig.CONFIG_SECTION_SELF_HOSTED}:DeploymentName");
        if (string.IsNullOrEmpty(deploymentName))
            deploymentName = "codellama:13b-code"; // Default to a Ollama CodeLlama-code model that seems to work OK (but not as good as OpenAI gpt-4o)
        var endpoint = await getCustomConfigJson($"{ApiConfig.CONFIG_SECTION_SELF_HOSTED}:Endpoint");
        if (string.IsNullOrEmpty(endpoint))
            endpoint = "http://localhost:11434/api"; // Default to local Ollama 
        Uri.TryCreate(endpoint, UriKind.Absolute, out var endPointUri);

        var apiConfig = new ApiConfig()
        {
            ApiKey = apiKey,    // Optional for Self-hosted model.
            DeploymentName = deploymentName, // AI CodeLlama-code model name (ex: codellama:13b-code, codellama:7b-code)
            Endpoint = endPointUri,     // Self-hosted OpenAI API compatible endpoint (for example Ollama)
            SelfHosted = true // Set to true to use self-hosted OpenAI API compatible endpoint.
        };
        return apiConfig;
    }

    public static async Task<CustomAIEndpointConfig> GetCustomAIEndpointConfig(Func<string, Task<string>> getCustomConfigJson)
    {
        var apiKey = await getCustomConfigJson($"{CustomAIEndpointConfig.CONFIG_SECTION}:ApiKey");
        if (string.IsNullOrEmpty(apiKey))
            apiKey = DEFAULT_CODE_ASSISTANT_CUSTOMENDPOINT_APIKEY;

        var endpoint = await getCustomConfigJson($"{CustomAIEndpointConfig.CONFIG_SECTION}:Endpoint");
        if (string.IsNullOrEmpty(endpoint))
            endpoint = DEFAULT_CODE_ASSISTANT_CUSTOMENDPOINT;
        Uri.TryCreate(endpoint, UriKind.Absolute, out var endPointUri);

        var apiConfig = new CustomAIEndpointConfig()
        {
            ApiKey = apiKey,
            Endpoint = endPointUri,
        };
        return apiConfig;
    }

    public static async Task SaveOpenAICodingAssistantConfigToLocalStorage(Func<string, string, Task> saveCustomConfigJson, ApiConfig apiConfig)
    {
        //await localStorageService.SetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION}:ApiKey", apiConfig.ApiKey ?? string.Empty);
        ////await localStorageService.SetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION}:Endpoint", apiConfig.Endpoint != null ? apiConfig.Endpoint.OriginalString : string.Empty);
    }

    public static async Task SaveOpenAISelfHostedCodeLlamaCodingAssistantConfigToLocalStorage(Func<string, string, Task> saveCustomConfigJson, ApiConfig apiConfig)
    {
        //await localStorageService.SetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION_SELF_HOSTED}:ApiKey", apiConfig.ApiKey ?? string.Empty);
        //await localStorageService.SetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION_SELF_HOSTED}:DeploymentName", apiConfig.DeploymentName ?? string.Empty);
        //await localStorageService.SetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION_SELF_HOSTED}:Endpoint", apiConfig.Endpoint != null ? apiConfig.Endpoint.OriginalString : string.Empty);
    }

    public static async Task SaveCustomCodingAssistantConfigToLocalStorage(Func<string, string, Task> saveCustomConfigJson, CustomAIEndpointConfig customAIEndpointConfig)
    {
        //await localStorageService.SetItemAsStringAsync($"{CustomAIEndpointConfig.CONFIG_SECTION}:ApiKey", customAIEndpointConfig.ApiKey ?? string.Empty);
        //await localStorageService.SetItemAsStringAsync($"{CustomAIEndpointConfig.CONFIG_SECTION}:Endpoint", customAIEndpointConfig.Endpoint != null ? customAIEndpointConfig.Endpoint.OriginalString : string.Empty);
    }
}
