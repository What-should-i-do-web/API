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
    /// HuggingFace Inference API provider implementation
    /// Supports chat completion and embeddings via the HF Inference API
    /// </summary>
    public class HuggingFaceProvider : IAIProvider
    {
        private readonly HttpClient _httpClient;
        private readonly HuggingFaceOptions _options;
        private readonly ILogger<HuggingFaceProvider> _logger;
        private const string DefaultBaseUrl = "https://api-inference.huggingface.co/models/";

        public string Name => "HuggingFace";
        public decimal CostPerThousandTokens => 0.0005m; // Inference API pricing (varies by model)

        public HuggingFaceProvider(
            HttpClient httpClient,
            IOptions<AIOptions> aiOptions,
            ILogger<HuggingFaceProvider> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = aiOptions?.Value?.HuggingFace ?? throw new ArgumentNullException(nameof(aiOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            var baseUrl = string.IsNullOrEmpty(_options.BaseUrl) ? DefaultBaseUrl : _options.BaseUrl;
            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            _httpClient.Timeout = TimeSpan.FromSeconds(60); // HF models can be slower
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
                // Combine system message and prompt
                var fullPrompt = string.IsNullOrEmpty(systemMessage)
                    ? prompt
                    : $"<|system|>\n{systemMessage}\n<|user|>\n{prompt}\n<|assistant|>";

                var requestBody = new
                {
                    inputs = fullPrompt,
                    parameters = new
                    {
                        max_new_tokens = maxTokens,
                        temperature,
                        return_full_text = false,
                        do_sample = temperature > 0
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Use the configured chat model (e.g., "mistralai/Mixtral-8x7B-Instruct-v0.1")
                var response = await _httpClient.PostAsync(_options.ChatModel, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("HuggingFace API error: {StatusCode} - {Error}", response.StatusCode, error);
                    throw new HttpRequestException($"HuggingFace API error: {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                // Parse response - HF returns array of generated text
                var responseArray = JsonSerializer.Deserialize<JsonElement[]>(responseContent);

                if (responseArray == null || responseArray.Length == 0)
                {
                    throw new InvalidOperationException("HuggingFace returned empty response");
                }

                var generatedText = responseArray[0].GetProperty("generated_text").GetString();

                _logger.LogDebug("HuggingFace completion successful: {Length} chars", generatedText?.Length ?? 0);

                return generatedText ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete chat with HuggingFace");
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
                    _logger.LogWarning(ex, "HuggingFace returned invalid JSON, attempting to extract");

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
                        throw new InvalidOperationException("Failed to extract valid JSON from HuggingFace response");
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete JSON with HuggingFace");
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
                    inputs = text
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Use embedding model (e.g., "sentence-transformers/all-MiniLM-L6-v2")
                var embeddingModel = _options.EmbeddingModel ?? "sentence-transformers/all-MiniLM-L6-v2";
                var response = await _httpClient.PostAsync(embeddingModel, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("HuggingFace embedding API error: {StatusCode} - {Error}", response.StatusCode, error);
                    throw new HttpRequestException($"HuggingFace embedding API error: {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                // Parse response - returns array of floats
                var embedding = JsonSerializer.Deserialize<float[]>(responseContent);

                if (embedding == null || embedding.Length == 0)
                {
                    throw new InvalidOperationException("HuggingFace returned empty embedding");
                }

                _logger.LogDebug("HuggingFace embedding generated: {Dimensions} dimensions", embedding.Length);

                return embedding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embedding with HuggingFace");
                throw;
            }
        }

        public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Simple health check - try to generate a small embedding
                var testEmbedding = await GenerateEmbeddingAsync("health check", cancellationToken);
                return testEmbedding != null && testEmbedding.Length > 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HuggingFace health check failed");
                return false;
            }
        }
    }
}
