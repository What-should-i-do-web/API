using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Infrastructure.Options;

namespace WhatShouldIDo.Infrastructure.Services.AI
{
    /// <summary>
    /// Ollama provider for local LLM deployment
    /// Supports running open-source models locally (Llama 2, Mistral, etc.)
    /// </summary>
    public class OllamaProvider : IAIProvider
    {
        private readonly HttpClient _httpClient;
        private readonly OllamaOptions _options;
        private readonly ILogger<OllamaProvider> _logger;
        private const string DefaultBaseUrl = "http://localhost:11434/api/";

        public string Name => "Ollama";
        public decimal CostPerThousandTokens => 0.0m; // Local deployment = free

        public OllamaProvider(
            HttpClient httpClient,
            IOptions<AIOptions> aiOptions,
            ILogger<OllamaProvider> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = aiOptions?.Value?.Ollama ?? throw new ArgumentNullException(nameof(aiOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            var baseUrl = string.IsNullOrEmpty(_options.BaseUrl) ? DefaultBaseUrl : _options.BaseUrl;
            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(120); // Local models can be slow on first run
        }

        public async Task<string> CompleteChatAsync(
            string prompt,
            string? systemMessage = null,
            double temperature = 0.7,
            int maxTokens = 1000,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Build messages array
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
                    stream = false,
                    options = new
                    {
                        temperature,
                        num_predict = maxTokens
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("chat", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Ollama API error: {StatusCode} - {Error}", response.StatusCode, error);

                    // Check if model needs to be pulled
                    if (error.Contains("model") && error.Contains("not found"))
                    {
                        _logger.LogWarning("Model {Model} not found. Please run: ollama pull {Model}", _options.ChatModel, _options.ChatModel);
                    }

                    throw new HttpRequestException($"Ollama API error: {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (!responseObj.TryGetProperty("message", out var message))
                {
                    throw new InvalidOperationException("Ollama returned invalid response format");
                }

                var generatedText = message.GetProperty("content").GetString();

                _logger.LogDebug("Ollama completion successful: {Length} chars", generatedText?.Length ?? 0);

                return generatedText ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete chat with Ollama");
                throw;
            }
        }

        public async Task<string> CompleteJsonAsync(
            string prompt,
            string? systemMessage = null,
            double temperature = 0.5,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Add JSON instruction to system message
                var jsonSystemMessage = string.IsNullOrEmpty(systemMessage)
                    ? "You are a helpful assistant that always responds in valid JSON format."
                    : $"{systemMessage}\n\nIMPORTANT: Respond ONLY with valid JSON. Do not include any explanation, markdown formatting, or text outside the JSON object.";

                var response = await CompleteChatAsync(prompt, jsonSystemMessage, temperature, 2000, cancellationToken);

                // Clean up response - remove markdown code blocks if present
                response = response.Trim();
                if (response.StartsWith("```json"))
                {
                    response = response.Substring(7);
                }
                if (response.StartsWith("```"))
                {
                    response = response.Substring(3);
                }
                if (response.EndsWith("```"))
                {
                    response = response.Substring(0, response.Length - 3);
                }

                response = response.Trim();

                // Validate it's JSON
                try
                {
                    JsonDocument.Parse(response);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Ollama returned invalid JSON, attempting to extract");

                    // Try to extract JSON from the response
                    var startIndex = response.IndexOf('{');
                    var endIndex = response.LastIndexOf('}');

                    if (startIndex >= 0 && endIndex > startIndex)
                    {
                        response = response.Substring(startIndex, endIndex - startIndex + 1);
                        JsonDocument.Parse(response); // Validate
                    }
                    else
                    {
                        throw new InvalidOperationException("Failed to extract valid JSON from Ollama response");
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete JSON with Ollama");
                throw;
            }
        }

        public async Task<float[]> GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var requestBody = new
                {
                    model = _options.EmbeddingModel ?? _options.ChatModel, // Use chat model if no embedding model specified
                    prompt = text
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("embeddings", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Ollama embedding API error: {StatusCode} - {Error}", response.StatusCode, error);
                    throw new HttpRequestException($"Ollama embedding API error: {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (!responseObj.TryGetProperty("embedding", out var embeddingElement))
                {
                    throw new InvalidOperationException("Ollama returned invalid embedding response");
                }

                var embedding = JsonSerializer.Deserialize<float[]>(embeddingElement.GetRawText());

                if (embedding == null || embedding.Length == 0)
                {
                    throw new InvalidOperationException("Ollama returned empty embedding");
                }

                _logger.LogDebug("Ollama embedding generated: {Dimensions} dimensions", embedding.Length);

                return embedding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embedding with Ollama");
                throw;
            }
        }

        public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if Ollama is running by hitting the tags endpoint
                var response = await _httpClient.GetAsync("tags", cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Ollama health check failed: {StatusCode}", response.StatusCode);
                    return false;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var tagsResponse = JsonSerializer.Deserialize<JsonElement>(content);

                // Check if our model is available
                if (tagsResponse.TryGetProperty("models", out var models))
                {
                    var modelsList = JsonSerializer.Deserialize<List<JsonElement>>(models.GetRawText());
                    var hasModel = modelsList?.Any(m =>
                    {
                        if (m.TryGetProperty("name", out var name))
                        {
                            return name.GetString()?.StartsWith(_options.ChatModel) == true;
                        }
                        return false;
                    }) ?? false;

                    if (!hasModel)
                    {
                        _logger.LogWarning("Ollama model {Model} not found. Please run: ollama pull {Model}",
                            _options.ChatModel, _options.ChatModel);
                    }

                    return hasModel;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ollama health check failed - service may not be running");
                return false;
            }
        }
    }
}
