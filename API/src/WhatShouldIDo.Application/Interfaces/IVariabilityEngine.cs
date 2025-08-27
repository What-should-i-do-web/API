using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.Interfaces
{
    public interface IVariabilityEngine
    {
        Task<List<Place>> FilterForVarietyAsync(Guid userId, List<Place> places, CancellationToken cancellationToken = default);
        Task<List<Place>> ApplyDiscoveryBoostAsync(Guid userId, List<Place> places, CancellationToken cancellationToken = default);
        Task<List<Place>> ApplySeasonalVarietyAsync(List<Place> places, CancellationToken cancellationToken = default);
        Task<List<Place>> ApplyContextualVarietyAsync(Guid userId, List<Place> places, string timeOfDay, string dayOfWeek, CancellationToken cancellationToken = default);
        Task<float> CalculateNoveltyScoreAsync(Guid userId, Place place, CancellationToken cancellationToken = default);
        Task<List<Place>> RankByVarietyAsync(Guid userId, List<Place> places, CancellationToken cancellationToken = default);
    }
}