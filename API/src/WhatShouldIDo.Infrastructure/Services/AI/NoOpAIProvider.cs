using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Services.AI
{
    /// <summary>
    /// No-operation AI provider for testing or when AI is disabled.
    /// Returns basic responses without external API calls.
    /// </summary>
    public class NoOpAIProvider : IAIProvider
    {
        private readonly ILogger<NoOpAIProvider> _logger;

        public string Name => "NoOp";
        public decimal CostPerThousandTokens => 0.0m;

        public NoOpAIProvider(ILogger<NoOpAIProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<string> CompleteChatAsync(
            string prompt,
            string? systemMessage = null,
            double temperature = 0.7,
            int maxTokens = 1000,
            CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("NoOp AI provider called for chat completion");
            return Task.FromResult("AI is currently disabled or unavailable.");
        }

        public Task<string> CompleteJsonAsync(
            string prompt,
            string? systemMessage = null,
            double temperature = 0.5,
            CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("NoOp AI provider called for JSON completion");
            // Return minimal valid JSON
            return Task.FromResult("{}");
        }

        public Task<float[]> GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("NoOp AI provider called for embedding generation");
            // Return a simple 384-dimensional zero vector
            return Task.FromResult(new float[384]);
        }

        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}
