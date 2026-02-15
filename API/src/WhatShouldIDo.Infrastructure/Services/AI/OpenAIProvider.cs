using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Infrastructure.Options;

namespace WhatShouldIDo.Infrastructure.Services.AI
{
    /// <summary>
    /// OpenAI API provider implementation using HTTP client
    /// </summary>
    public class OpenAIProvider : IAIProvider
    {
        private readonly HttpClient _httpClient;
        private readonly OpenAIOptions _options;
        private readonly ILogger<OpenAIProvider> _logger;
        private const string DefaultBaseUrl = "https://api.openai.com/v1/";

        public string Name => "OpenAI";
        public decimal CostPerThousandTokens => 0.0015m; // gpt-4o-mini pricing

        public OpenAIProvider(
            HttpClient httpClient,
            IOptions<AIOptions> aiOptions,
            ILogger<OpenAIProvider> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = aiOptions?.Value?.OpenAI ?? throw new ArgumentNullException(nameof(aiOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            var baseUrl = string.IsNullOrEmpty(_options.BaseUrl) ? DefaultBaseUrl : _options.BaseUrl;
            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            if (!string.IsNullOrEmpty(_options.OrganizationId))
            {
                _httpClient.DefaultRequestHeaders.Add("OpenAI-Organization", _options.OrganizationId);
            }

            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<string> CompleteChatAsync(
            string prompt,
            string? systemMessage = null,
            double temperature = 0.7,
            int maxTokens = 1000,
            CancellationToken cancellationToken = default)
        {
            var messages = new List<object>();

            if (!string.IsNullOrEmpty(systemMessage))
            {
                messages.Add(new { role = "system", content = systemMessage });
            }

            messages.Add(new { role = "user", content = prompt });

            var requestBody = new
            {
                model = _options.ChatModel,
                messages,
                temperature,
                max_tokens = maxTokens
            };

            try
            {
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("chat/completions", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonDocument.Parse(responseJson);

                var messageContent = result.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                _logger.LogDebug("OpenAI chat completion successful");
                return messageContent ?? string.Empty;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "OpenAI API request failed");
                throw new InvalidOperationException("Failed to communicate with OpenAI API", ex);
            }
        }

        public async Task<string> CompleteJsonAsync(
            string prompt,
            string? systemMessage = null,
            double temperature = 0.5,
            CancellationToken cancellationToken = default)
        {
            var messages = new List<object>();

            if (!string.IsNullOrEmpty(systemMessage))
            {
                messages.Add(new { role = "system", content = systemMessage + "\n\nIMPORTANT: Return ONLY valid JSON, no additional text." });
            }
            else
            {
                messages.Add(new { role = "system", content = "Return ONLY valid JSON, no additional text." });
            }

            messages.Add(new { role = "user", content = prompt });

            var requestBody = new
            {
                model = _options.ChatModel,
                messages,
                temperature,
                max_tokens = 2000,
                response_format = new { type = "json_object" } // Ensures JSON response
            };

            try
            {
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/chat/completions", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonDocument.Parse(responseJson);

                var messageContent = result.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                _logger.LogDebug("OpenAI JSON completion successful");
                return messageContent ?? "{}";
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "OpenAI API request failed for JSON completion");
                throw new InvalidOperationException("Failed to communicate with OpenAI API", ex);
            }
        }

        public async Task<float[]> GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            var requestBody = new
            {
                model = _options.EmbeddingModel,
                input = text
            };

            try
            {
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("embeddings", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonDocument.Parse(responseJson);

                var embeddingArray = result.RootElement
                    .GetProperty("data")[0]
                    .GetProperty("embedding");

                var embedding = new List<float>();
                foreach (var element in embeddingArray.EnumerateArray())
                {
                    embedding.Add((float)element.GetDouble());
                }

                _logger.LogDebug("OpenAI embedding generated successfully");
                return embedding.ToArray();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "OpenAI API request failed for embedding generation");
                throw new InvalidOperationException("Failed to communicate with OpenAI API", ex);
            }
        }

        public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Simple health check: try to list models
                var response = await _httpClient.GetAsync("models", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
