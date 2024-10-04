// Based on https://github.com/dotnet/smartcomponents

using Microsoft.Extensions.Configuration;

namespace Highbyte.DotNet6502.AI.CodingAssistant.Inference.OpenAI;

public class ApiConfig
{
    public string? ApiKey { get; set; }
    public string? DeploymentName { get; set; }
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

    public bool SelfHosted { get; set; }

    public const string CONFIG_SECTION = "CodingAssistant:OpenAI";
    public const string CONFIG_SECTION_SELF_HOSTED = "CodingAssistant:OpenAISelfHostedCodeLlama";

    public ApiConfig()
    {
    }

    public ApiConfig(IConfiguration config, bool selfHosted)
    {
        // Using OpenAI API
        if (selfHosted)
        {
            //Self-hosted API compatible with OpenAI (with CodeLllama-code model),
            SelfHosted = true;

            var configSection = config.GetRequiredSection(CONFIG_SECTION_SELF_HOSTED);

            Endpoint = configSection.GetValue<Uri>("Endpoint")
                ?? throw new InvalidOperationException($"Missing required configuration value: {CONFIG_SECTION_SELF_HOSTED}:Endpoint. This is required for SelfHosted inference.");

            // Ollama uses this, but other self-hosted backends might not, so it's optional.
            DeploymentName = configSection.GetValue<string>("DeploymentName");

            // Ollama doesn't use this, but other self-hosted backends might do, so it's optional.
            ApiKey = configSection.GetValue<string>("ApiKey");
        }
        else
        {
            // OpenAI or Azure OpenAI
            SelfHosted = false;

            var configSection = config.GetRequiredSection(CONFIG_SECTION);

            // If set, we assume Azure OpenAI. If not, we assume OpenAI.
            Endpoint = configSection.GetValue<Uri>("Endpoint");

            // For Azure OpenAI, it's your deployment name. For OpenAI, it's the model name.
            DeploymentName = configSection.GetValue<string>("DeploymentName")
                ?? throw new InvalidOperationException($"Missing required configuration value: {CONFIG_SECTION}:DeploymentName");

            ApiKey = configSection.GetValue<string>("ApiKey")
                ?? throw new InvalidOperationException($"Missing required configuration value: {CONFIG_SECTION}:ApiKey");
        }
    }
}
