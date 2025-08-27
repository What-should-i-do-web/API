using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.Interfaces
{
    public interface IVisitTrackingService
    {
        Task LogSuggestionViewAsync(Guid userId, Place place, string reason, CancellationToken cancellationToken = default);
        Task LogVisitConfirmationAsync(Guid userId, Guid placeId, int? durationMinutes = null, CancellationToken cancellationToken = default);
        Task LogUserFeedbackAsync(Guid userId, Guid placeId, float rating, string? review = null, bool wouldRecommend = true, CancellationToken cancellationToken = default);
        Task<List<UserVisit>> GetUserVisitHistoryAsync(Guid userId, int days = 30, CancellationToken cancellationToken = default);
        Task<List<Place>> GetRecentlyVisitedPlacesAsync(Guid userId, int days = 30, CancellationToken cancellationToken = default);
        Task<bool> HasUserVisitedPlaceAsync(Guid userId, Guid placeId, int days = 30, CancellationToken cancellationToken = default);
        Task<Dictionary<string, int>> GetUserCategoryPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<float> GetPlaceAvoidanceScoreAsync(Guid userId, Place place, CancellationToken cancellationToken = default);
    }
}