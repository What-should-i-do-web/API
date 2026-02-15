using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.Interfaces
{
    public interface ISmartSuggestionService
    {
        Task<List<SuggestionDto>> GetPersonalizedSuggestionsAsync(Guid userId, PromptRequest request, CancellationToken cancellationToken = default);
        Task<List<SuggestionDto>> GetPersonalizedNearbySuggestionsAsync(Guid userId, float lat, float lng, int radius, CancellationToken cancellationToken = default);
        Task<SuggestionDto> GetPersonalizedRandomSuggestionAsync(Guid userId, float lat, float lng, int radius, CancellationToken cancellationToken = default);
        Task<List<SuggestionDto>> ApplyPersonalizationAsync(Guid userId, List<Place> places, string originalPrompt, CancellationToken cancellationToken = default);
        Task LogSuggestionInteractionAsync(Guid userId, Guid suggestionId, string interactionType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a "Surprise Me" personalized route with AI-assisted diversification and route optimization.
        /// Implements exclusion window logic, respects user exclusions/favorites, and persists to history.
        /// </summary>
        /// <param name="userId">User ID requesting the surprise route</param>
        /// <param name="request">Surprise Me request parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Optimized route with personalization metadata</returns>
        Task<SurpriseMeResponse> GenerateSurpriseRouteAsync(
            Guid userId,
            SurpriseMeRequest request,
            CancellationToken cancellationToken = default);
    }
}