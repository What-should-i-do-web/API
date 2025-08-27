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
    }
}