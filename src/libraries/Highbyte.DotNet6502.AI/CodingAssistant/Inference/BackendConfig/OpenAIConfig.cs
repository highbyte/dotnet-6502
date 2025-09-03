using Microsoft.Extensions.Configuration;

namespace Highbyte.DotNet6502.AI.CodingAssistant.Inference.BackendConfig;

public class OpenAIConfig
{
    public string ApiKey { get; set; }
    public string ModelName { get; set; }

    public const string CONFIG_SECTION = "CodingAssistant:OpenAI";

    public OpenAIConfig()
    {
    }

    public OpenAIConfig(IConfiguration config)
    {
        var configSection = config.GetRequiredSection(CONFIG_SECTION);
        ModelName = configSection.GetValue<string>("ModelName")
            ?? throw new InvalidOperationException($"Missing required configuration value: {CONFIG_SECTION}:ModelName");
        ApiKey = configSection.GetValue<string>("ApiKey")
            ?? throw new InvalidOperationException($"Missing required configuration value: {CONFIG_SECTION}:ApiKey");
    }
}
