using Microsoft.Extensions.Configuration;

namespace Highbyte.DotNet6502.AI.CodingAssistant.Inference.BackendConfig;

public class AzureOpenAIConfig
{
    public string ModelName { get; set; }
    public Uri Endpoint { get; set; }


    public const string CONFIG_SECTION = "CodingAssistant:AzureOpenAI";

    public AzureOpenAIConfig()
    {
    }

    public AzureOpenAIConfig(IConfiguration config)
    {
        var configSection = config.GetRequiredSection(CONFIG_SECTION);
        Endpoint = configSection.GetValue<Uri>("Endpoint")
            ?? throw new InvalidOperationException($"Missing required configuration value: {CONFIG_SECTION}:Endpoint");
        ModelName = configSection.GetValue<string>("ModelName")
            ?? throw new InvalidOperationException($"Missing required configuration value: {CONFIG_SECTION}:ModelName");
    }
}
