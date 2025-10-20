using System.Text.Json.Serialization;

namespace LORE_LLM.Application.Chat;

public sealed record ChatProvidersConfiguration
{
    [JsonPropertyName("defaultProvider")]
    public string? DefaultProvider { get; init; }

    [JsonPropertyName("providers")]
    public ProvidersSection Providers { get; init; } = new();

    public sealed record ProvidersSection
    {
        [JsonPropertyName("deepseek")]
        public DeepSeekProviderConfiguration? DeepSeek { get; init; }
    }
}

public sealed record DeepSeekProviderConfiguration
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    [JsonPropertyName("maxTokens")]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("apiKeyEnvVar")]
    public string? ApiKeyEnvVar { get; init; }

    // Optional fallback value; environment variables take precedence.
    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; init; }
}


