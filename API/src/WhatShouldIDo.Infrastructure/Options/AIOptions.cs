using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Infrastructure.Options
{
    /// <summary>
    /// Configuration options for AI service providers
    /// </summary>
    public class AIOptions
    {
        public const string SectionName = "AI";

        /// <summary>
        /// Whether AI features are enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Primary AI provider to use ("OpenAI", "HuggingFace", "Ollama", "AzureAI", "None")
        /// </summary>
        [Required]
        public string Provider { get; set; } = "OpenAI";

        /// <summary>
        /// Fallback provider if primary fails
        /// </summary>
        public string? FallbackProvider { get; set; }

        /// <summary>
        /// OpenAI configuration
        /// </summary>
        public OpenAIOptions OpenAI { get; set; } = new();

        /// <summary>
        /// HuggingFace configuration
        /// </summary>
        public HuggingFaceOptions HuggingFace { get; set; } = new();

        /// <summary>
        /// Ollama configuration
        /// </summary>
        public OllamaOptions Ollama { get; set; } = new();

        /// <summary>
        /// Azure AI configuration
        /// </summary>
        public AzureAIOptions AzureAI { get; set; } = new();

        /// <summary>
        /// Default temperature for completions (0.0-1.0)
        /// </summary>
        public double DefaultTemperature { get; set; } = 0.7;

        /// <summary>
        /// Default maximum tokens for responses
        /// </summary>
        public int DefaultMaxTokens { get; set; } = 1000;

        /// <summary>
        /// Timeout for AI requests in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Whether to cache AI responses
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// Cache TTL in minutes
        /// </summary>
        public int CacheTTLMinutes { get; set; } = 60;

        /// <summary>
        /// Provider priority configuration for different capabilities
        /// </summary>
        public ProviderPriorityOptions? ProviderPriority { get; set; }
    }

    /// <summary>
    /// Configuration for provider priority per capability
    /// </summary>
    public class ProviderPriorityOptions
    {
        /// <summary>
        /// Provider priority for chat/completion operations
        /// </summary>
        public List<string> Chat { get; set; } = new() { "OpenAI", "Ollama", "NoOp" };

        /// <summary>
        /// Provider priority for embedding operations
        /// </summary>
        public List<string> Embedding { get; set; } = new() { "OpenAI", "Ollama", "NoOp" };
    }

    public class OpenAIOptions
    {
        /// <summary>
        /// OpenAI API key
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Organization ID (optional)
        /// </summary>
        public string? OrganizationId { get; set; }

        /// <summary>
        /// Model for chat completions (default: gpt-4o-mini)
        /// </summary>
        public string ChatModel { get; set; } = "gpt-4o-mini";

        /// <summary>
        /// Model for embeddings (default: text-embedding-3-small)
        /// </summary>
        public string EmbeddingModel { get; set; } = "text-embedding-3-small";

        /// <summary>
        /// API base URL (for Azure OpenAI or custom endpoints)
        /// </summary>
        public string? BaseUrl { get; set; }
    }

    public class HuggingFaceOptions
    {
        /// <summary>
        /// HuggingFace API token/key
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Model ID for chat/inference (e.g., mistralai/Mixtral-8x7B-Instruct-v0.1)
        /// </summary>
        public string ChatModel { get; set; } = "mistralai/Mixtral-8x7B-Instruct-v0.1";

        /// <summary>
        /// Inference API base URL
        /// </summary>
        public string? BaseUrl { get; set; }

        /// <summary>
        /// Embedding model ID (e.g., sentence-transformers/all-MiniLM-L6-v2)
        /// </summary>
        public string? EmbeddingModel { get; set; }
    }

    public class OllamaOptions
    {
        /// <summary>
        /// Ollama base URL (default: http://localhost:11434/api/)
        /// </summary>
        public string? BaseUrl { get; set; }

        /// <summary>
        /// Model name for chat (e.g., "llama2", "mistral", "mixtral")
        /// </summary>
        public string ChatModel { get; set; } = "llama2";

        /// <summary>
        /// Model name for embeddings (optional, uses chat model if not specified)
        /// </summary>
        public string? EmbeddingModel { get; set; }
    }

    public class AzureAIOptions
    {
        /// <summary>
        /// Azure OpenAI endpoint
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Azure OpenAI API key
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Deployment name for chat
        /// </summary>
        public string ChatDeployment { get; set; } = string.Empty;

        /// <summary>
        /// Deployment name for embeddings
        /// </summary>
        public string EmbeddingDeployment { get; set; } = string.Empty;

        /// <summary>
        /// API version
        /// </summary>
        public string ApiVersion { get; set; } = "2024-02-01";
    }
}
