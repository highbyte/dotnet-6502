using Microsoft.Extensions.Configuration;

namespace Highbyte.DotNet6502.AI.CodingAssistant;

public partial class CustomAIEndpointCodeSuggestion
{
    public class CustomAIEndpointConfig
    {
        public string? ApiKey { get; set; }

        public Uri? Endpoint { get; set; }

        public const string CONFIG_SECTION = "CodingAssistant:CustomEndpoint";

        public const string DEFAULT_ENDPOINT = "https://highbyte-dotnet6502-codecompletion.azurewebsites.net/";
        // Note: DEFAULT_API_KEY is the "public" API key for the custom endpoint, separate from others.
        public const string DEFAULT_API_KEY = "9fe8f8161c1d43251a46bb576336a1a25d7ab607cb5a1b4b960c0949d87bced7";

        public CustomAIEndpointConfig()
        {
        }

        public CustomAIEndpointConfig(IConfiguration config)
        {
            var configSection = config.GetSection(CONFIG_SECTION);
            Endpoint = configSection.GetValue<Uri>("Endpoint");
            ApiKey = configSection.GetValue<string>("ApiKey");
        }

        public void WriteToConfiguration(IConfiguration config)
        {
            var configSection = config.GetSection(CONFIG_SECTION);
            configSection["Endpoint"] = Endpoint?.OriginalString;
            configSection["ApiKey"] = ApiKey;
        }

        public IConfigurationSection GetConfigurationSection(IConfiguration config)
        {
            return config.GetSection(CONFIG_SECTION);
        }
    }
}
