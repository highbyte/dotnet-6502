using Microsoft.Extensions.Configuration;

namespace Highbyte.DotNet6502.AI.CodingAssistant.Inference.BackendConfig;

public class OllamaConfig
{
    public string? ApiKey { get; set; }
    public string? ModelName { get; set; }
    public string EndpointString
    {
        get
        {
            return Endpoint?.ToString() ?? string.Empty;
        }
        set
        {
            Endpoint = string.IsNullOrWhiteSpace(value) ? null : new Uri(value);
        }
    }
    public Uri? Endpoint { get; set; }

    public const string CONFIG_SECTION = "CodingAssistant:OpenAISelfHostedCodeLlama";

    public OllamaConfig()
    {
    }

    public OllamaConfig(IConfiguration config)
    {
        var configSection = config.GetRequiredSection(CONFIG_SECTION);

        Endpoint = configSection.GetValue<Uri>("Endpoint")
            ?? throw new InvalidOperationException($"Missing required configuration value: {CONFIG_SECTION}:Endpoint. This is required for SelfHosted Ollama inference.");
        ModelName = configSection.GetValue<string>("ModelName")
            ?? throw new InvalidOperationException($"Missing required configuration value: {CONFIG_SECTION}:ModelName. This is required for SelfHosted Ollama inference.");
        // ApiKey is optional. Only used if there is a proxy like Open WebUI in front of Ollama API.
        ApiKey = configSection.GetValue<string>("ApiKey");
    }
}
