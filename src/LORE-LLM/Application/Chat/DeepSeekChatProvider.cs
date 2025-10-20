using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;

namespace LORE_LLM.Application.Chat;

public sealed class DeepSeekChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public string Name => "deepseek";

    public DeepSeekChatProvider(HttpClient httpClient, string apiKey, string model = "deepseek-chat")
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
    }

    public async Task<Result<string>> CompleteAsync(string prompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return Result.Failure<string>("DeepSeek API key not configured. Set DEEPSEEK_API_KEY environment variable.");
        }

        try
        {
            var request = new DeepSeekRequest
            {
                Model = _model,
                Messages = new[]
                {
                    new DeepSeekMessage { Role = "user", Content = prompt }
                },
                Temperature = 0.7,
                MaxTokens = 4096
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.deepseek.com/v1/chat/completions")
            {
                Content = JsonContent.Create(request, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                })
            };
            httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return Result.Failure<string>($"DeepSeek API error: {response.StatusCode} - {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<DeepSeekResponse>(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }, cancellationToken);

            if (result?.Choices is null || result.Choices.Length == 0)
            {
                return Result.Failure<string>("DeepSeek API returned no choices.");
            }

            var content = result.Choices[0].Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                return Result.Failure<string>("DeepSeek API returned empty content.");
            }

            return Result.Success(content);
        }
        catch (Exception ex)
        {
            return Result.Failure<string>($"DeepSeek API call failed: {ex.Message}");
        }
    }

    private sealed record DeepSeekRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("messages")]
        public required DeepSeekMessage[] Messages { get; init; }

        [JsonPropertyName("temperature")]
        public double? Temperature { get; init; }

        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; init; }
    }

    private sealed record DeepSeekMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; init; }

        [JsonPropertyName("content")]
        public required string Content { get; init; }
    }

    private sealed record DeepSeekResponse
    {
        [JsonPropertyName("choices")]
        public DeepSeekChoice[]? Choices { get; init; }
    }

    private sealed record DeepSeekChoice
    {
        [JsonPropertyName("message")]
        public DeepSeekMessage? Message { get; init; }
    }
}

