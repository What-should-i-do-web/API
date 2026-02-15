namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Low-level AI provider abstraction for different AI backends.
    /// Implementations should handle provider-specific logic (API calls, authentication, etc.)
    /// </summary>
    public interface IAIProvider
    {
        /// <summary>
        /// Provider name (e.g., "OpenAI", "HuggingFace", "Ollama", "Azure AI")
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Sends a completion request to the AI provider
        /// </summary>
        /// <param name="prompt">The prompt text</param>
        /// <param name="systemMessage">Optional system message for context</param>
        /// <param name="temperature">Creativity level (0.0 = deterministic, 1.0 = creative)</param>
        /// <param name="maxTokens">Maximum response tokens</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>AI-generated response text</returns>
        Task<string> CompleteChatAsync(
            string prompt,
            string? systemMessage = null,
            double temperature = 0.7,
            int maxTokens = 1000,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a structured JSON completion request
        /// </summary>
        /// <param name="prompt">The prompt text</param>
        /// <param name="systemMessage">Optional system message</param>
        /// <param name="temperature">Creativity level</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>AI-generated JSON response</returns>
        Task<string> CompleteJsonAsync(
            string prompt,
            string? systemMessage = null,
            double temperature = 0.5,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates embeddings for semantic similarity
        /// </summary>
        /// <param name="text">Text to embed</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Vector embedding</returns>
        Task<float[]> GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the provider is available and healthy
        /// </summary>
        Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the estimated cost per 1000 tokens for this provider
        /// </summary>
        decimal CostPerThousandTokens { get; }
    }
}
