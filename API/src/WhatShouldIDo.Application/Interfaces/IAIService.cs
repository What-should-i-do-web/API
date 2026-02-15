using WhatShouldIDo.Application.DTOs.AI;
using WhatShouldIDo.Application.DTOs.Prompt;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// AI Service abstraction for provider-agnostic AI operations.
    /// Supports multiple AI providers (OpenAI, HuggingFace, Ollama, Azure AI, etc.)
    /// </summary>
    public interface IAIService
    {
        /// <summary>
        /// Interprets natural language prompt into structured search filters.
        /// Example: "cheap vegan restaurants near Kadıköy" -> { categories: ["restaurant"], dietary: ["vegan"], priceLevel: "inexpensive", location: "Kadıköy" }
        /// </summary>
        Task<InterpretedPrompt> InterpretPromptAsync(string promptText, CancellationToken cancellationToken = default);

        /// <summary>
        /// Re-ranks a list of places based on semantic similarity to the original query.
        /// Uses embeddings or semantic analysis to improve result relevance.
        /// </summary>
        Task<List<PlaceDto>> RankPlacesByRelevanceAsync(
            string originalQuery,
            List<PlaceDto> places,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a summary or description for a place based on its reviews and metadata.
        /// Useful for generating concise, informative summaries.
        /// </summary>
        Task<PlaceSummary> SummarizePlaceAsync(
            PlaceDto place,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates an AI-driven daily itinerary based on user preferences and available places.
        /// Returns a structured plan with ordered activities, timing, and reasoning.
        /// </summary>
        Task<AIItinerary> GenerateDailyItineraryAsync(
            AIItineraryRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts structured information from unstructured text.
        /// Useful for parsing user preferences, reviews, or other text data.
        /// </summary>
        Task<Dictionary<string, string>> ExtractStructuredDataAsync(
            string text,
            string[] fields,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates an embedding vector for the given text.
        /// Used for semantic similarity, personalization, and vector search.
        /// </summary>
        /// <param name="text">Text to embed</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Vector embedding (float array)</returns>
        Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the AI service is available and healthy.
        /// </summary>
        Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the name of the current AI provider being used.
        /// </summary>
        string ProviderName { get; }
    }
}
