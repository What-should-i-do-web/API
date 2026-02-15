using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Infrastructure.Options;

namespace WhatShouldIDo.Infrastructure.Services.AI
{
    /// <summary>
    /// Factory for creating AI providers based on configuration
    /// </summary>
    public class AIProviderFactory
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AIOptions _options;
        private readonly ILogger<AIProviderFactory> _logger;

        public AIProviderFactory(
            IServiceScopeFactory scopeFactory,
            IOptions<AIOptions> options,
            ILogger<AIProviderFactory> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates the primary AI provider based on configuration
        /// </summary>
        public IAIProvider CreatePrimaryProvider()
        {
            if (!_options.Enabled)
            {
                _logger.LogWarning("AI is disabled, using NoOp provider");
                return ResolveProviderScoped<NoOpAIProvider>();
            }

            return CreateProvider(_options.Provider);
        }

        /// <summary>
        /// Creates the fallback AI provider if configured
        /// </summary>
        public IAIProvider? CreateFallbackProvider()
        {
            if (string.IsNullOrEmpty(_options.FallbackProvider))
                return null;

            try
            {
                return CreateProvider(_options.FallbackProvider);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create fallback provider: {Provider}", _options.FallbackProvider);
                return null;
            }
        }

        private IAIProvider CreateProvider(string providerName)
        {
            _logger.LogInformation("Creating AI provider: {Provider}", providerName);

            return providerName.ToLowerInvariant() switch
            {
                "openai" => CreateOpenAIProvider(),
                "huggingface" => CreateHuggingFaceProvider(),
                "ollama" => CreateOllamaProvider(),
                "azureai" or "azure" => CreateAzureAIProvider(),
                "none" or "noop" => ResolveProviderScoped<NoOpAIProvider>(),
                _ => throw new InvalidOperationException($"Unknown AI provider: {providerName}")
            };
        }

        private IAIProvider CreateOpenAIProvider()
        {
            if (string.IsNullOrEmpty(_options.OpenAI.ApiKey))
            {
                _logger.LogWarning("OpenAI API key not configured, using NoOp provider");
                return ResolveProviderScoped<NoOpAIProvider>();
            }

            return ResolveProviderScoped<OpenAIProvider>();
        }

        private IAIProvider CreateHuggingFaceProvider()
        {
            if (string.IsNullOrEmpty(_options.HuggingFace.ApiKey))
            {
                _logger.LogWarning("HuggingFace API key not configured, using NoOp provider");
                return ResolveProviderScoped<NoOpAIProvider>();
            }

            _logger.LogInformation("Creating HuggingFace provider with model: {Model}", _options.HuggingFace.ChatModel);
            return ResolveProviderScoped<HuggingFaceProvider>();
        }

        private IAIProvider CreateOllamaProvider()
        {
            _logger.LogInformation("Creating Ollama provider with model: {Model} at {BaseUrl}",
                _options.Ollama.ChatModel, _options.Ollama.BaseUrl ?? "http://localhost:11434");
            return ResolveProviderScoped<OllamaProvider>();
        }

        private IAIProvider CreateAzureAIProvider()
        {
            _logger.LogWarning("Azure AI provider not yet implemented, using NoOp");
            return ResolveProviderScoped<NoOpAIProvider>();
        }

        /// <summary>
        /// Helper to resolve provider in a scoped lifetime
        /// </summary>
        private T ResolveProviderScoped<T>() where T : notnull
        {
            using var scope = _scopeFactory.CreateScope();
            return scope.ServiceProvider.GetRequiredService<T>();
        }
    }
}
