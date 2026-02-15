using Microsoft.EntityFrameworkCore;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Infrastructure.Services
{
    /// <summary>
    /// Manages taste profiles including feedback-driven evolution with optimistic concurrency.
    /// </summary>
    public class TasteProfileService : ITasteProfileService
    {
        private readonly ITasteProfileRepository _profileRepository;
        private readonly ITasteEventRepository _eventRepository;
        private readonly IPlaceCategoryMapper _categoryMapper;
        private readonly IClock _clock;
        private const int MaxConcurrencyRetries = 3;

        public TasteProfileService(
            ITasteProfileRepository profileRepository,
            ITasteEventRepository eventRepository,
            IPlaceCategoryMapper categoryMapper,
            IClock clock)
        {
            _profileRepository = profileRepository;
            _eventRepository = eventRepository;
            _categoryMapper = categoryMapper;
            _clock = clock;
        }

        public async Task<TasteProfileDto?> GetProfileAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var profile = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);
            if (profile == null)
                return null;

            return MapToDto(profile);
        }

        public async Task<UserTasteProfile?> GetProfileEntityAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await _profileRepository.GetByUserIdAsync(userId, cancellationToken);
        }

        public async Task<TasteProfileSummaryDto> GetProfileSummaryAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var profile = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);

            if (profile == null)
            {
                // Return default neutral profile
                return new TasteProfileSummaryDto
                {
                    UserId = userId,
                    QuizVersion = "none",
                    Interests = new Dictionary<string, double>
                    {
                        ["Culture"] = 0.5,
                        ["Food"] = 0.5,
                        ["Nature"] = 0.5,
                        ["Nightlife"] = 0.5,
                        ["Shopping"] = 0.5,
                        ["Art"] = 0.5,
                        ["Wellness"] = 0.5,
                        ["Sports"] = 0.5
                    },
                    Preferences = new Dictionary<string, double>
                    {
                        ["TasteQuality"] = 0.5,
                        ["Atmosphere"] = 0.5,
                        ["Design"] = 0.5,
                        ["Calmness"] = 0.5,
                        ["Spaciousness"] = 0.5
                    },
                    NoveltyTolerance = 0.5,
                    LastUpdatedAt = DateTime.UtcNow
                };
            }

            return new TasteProfileSummaryDto
            {
                UserId = profile.UserId,
                QuizVersion = profile.QuizVersion,
                Interests = new Dictionary<string, double>
                {
                    ["Culture"] = profile.CultureWeight,
                    ["Food"] = profile.FoodWeight,
                    ["Nature"] = profile.NatureWeight,
                    ["Nightlife"] = profile.NightlifeWeight,
                    ["Shopping"] = profile.ShoppingWeight,
                    ["Art"] = profile.ArtWeight,
                    ["Wellness"] = profile.WellnessWeight,
                    ["Sports"] = profile.SportsWeight
                },
                Preferences = new Dictionary<string, double>
                {
                    ["TasteQuality"] = profile.TasteQualityWeight,
                    ["Atmosphere"] = profile.AtmosphereWeight,
                    ["Design"] = profile.DesignWeight,
                    ["Calmness"] = profile.CalmnessWeight,
                    ["Spaciousness"] = profile.SpaciousnessWeight
                },
                NoveltyTolerance = profile.NoveltyTolerance,
                LastUpdatedAt = profile.UpdatedAtUtc
            };
        }

        public async Task<TasteProfileDto> UpdateProfileAsync(
            Guid userId,
            Dictionary<string, double> weights,
            CancellationToken cancellationToken = default)
        {
            // Retry loop for optimistic concurrency
            for (int attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
            {
                try
                {
                    var profile = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);

                    if (profile == null)
                    {
                        throw new InvalidOperationException($"No taste profile found for user {userId}");
                    }

                    // Manual update (not bounded)
                    profile.UpdateWeights(weights, _clock.UtcNow);

                    var updated = await _profileRepository.UpdateAsync(profile, cancellationToken);

                    // Record event
                    var manualEvent = UserTasteEvent.ManualEdit(
                        userId,
                        weights,
                        _clock.UtcNow
                    );

                    await _eventRepository.AddAsync(manualEvent, cancellationToken);

                    return MapToDto(updated);
                }
                catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries - 1)
                {
                    await Task.Delay(50 * (attempt + 1), cancellationToken);
                    continue;
                }
            }

            throw new InvalidOperationException("Failed to update profile after multiple retries due to concurrent updates");
        }

        public async Task<PlaceFeedbackResponse> ApplyFeedbackAsync(
            Guid userId,
            PlaceFeedbackRequest feedbackRequest,
            CancellationToken cancellationToken = default)
        {
            // Retry loop for optimistic concurrency
            for (int attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
            {
                try
                {
                    // Get current profile
                    var profile = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);

                    if (profile == null)
                    {
                        return new PlaceFeedbackResponse
                        {
                            Success = false,
                            Message = "No taste profile found. Please complete the quiz first."
                        };
                    }

                    // Compute weight deltas from feedback
                    var deltas = ComputeFeedbackDeltas(feedbackRequest.PlaceCategory, feedbackRequest.FeedbackType);

                    // Apply bounded deltas
                    profile.ApplyDelta(deltas, _clock.UtcNow);

                    // Save with concurrency check
                    var updated = await _profileRepository.UpdateAsync(profile, cancellationToken);

                    // Record event
                    var eventType = feedbackRequest.FeedbackType.ToLower() switch
                    {
                        "like" => UserTasteEvent.EventTypes.FeedbackLike,
                        "dislike" => UserTasteEvent.EventTypes.FeedbackDislike,
                        "skip" => UserTasteEvent.EventTypes.FeedbackSkip,
                        _ => UserTasteEvent.EventTypes.FeedbackLike
                    };

                    var feedbackEvent = UserTasteEvent.FeedbackReceived(
                        userId,
                        feedbackRequest.PlaceCategory,
                        eventType,
                        deltas,
                        _clock.UtcNow
                    );

                    await _eventRepository.AddAsync(feedbackEvent, cancellationToken);

                    // Build response
                    return new PlaceFeedbackResponse
                    {
                        Success = true,
                        UpdatedProfile = new TasteProfileSummaryDto
                        {
                            UserId = updated.UserId,
                            QuizVersion = updated.QuizVersion,
                            Interests = new Dictionary<string, double>
                            {
                                ["Culture"] = updated.CultureWeight,
                                ["Food"] = updated.FoodWeight,
                                ["Nature"] = updated.NatureWeight,
                                ["Nightlife"] = updated.NightlifeWeight,
                                ["Shopping"] = updated.ShoppingWeight,
                                ["Art"] = updated.ArtWeight,
                                ["Wellness"] = updated.WellnessWeight,
                                ["Sports"] = updated.SportsWeight
                            },
                            Preferences = new Dictionary<string, double>
                            {
                                ["TasteQuality"] = updated.TasteQualityWeight,
                                ["Atmosphere"] = updated.AtmosphereWeight,
                                ["Design"] = updated.DesignWeight,
                                ["Calmness"] = updated.CalmnessWeight,
                                ["Spaciousness"] = updated.SpaciousnessWeight
                            },
                            NoveltyTolerance = updated.NoveltyTolerance,
                            LastUpdatedAt = updated.UpdatedAtUtc
                        },
                        AppliedDeltas = deltas,
                        Message = $"Profile updated based on your {feedbackRequest.FeedbackType} feedback"
                    };
                }
                catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries - 1)
                {
                    // Retry on concurrency conflict
                    await Task.Delay(50 * (attempt + 1), cancellationToken); // Exponential backoff
                    continue;
                }
            }

            return new PlaceFeedbackResponse
            {
                Success = false,
                Message = "Failed to apply feedback after multiple retries due to concurrent updates"
            };
        }

        public async Task<bool> HasProfileAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var profile = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);
            return profile != null;
        }

        public async Task DeleteProfileAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var profile = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);
            if (profile != null)
            {
                await _profileRepository.DeleteAsync(profile.Id, cancellationToken);
            }
        }

        public async Task<List<UserTasteEvent>> GetEventHistoryAsync(
            Guid userId,
            int limit = 50,
            CancellationToken cancellationToken = default)
        {
            return await _eventRepository.GetByUserIdAsync(userId, 0, limit, cancellationToken);
        }

        /// <summary>
        /// Compute weight deltas based on place category and feedback type.
        /// Uses PlaceCategoryMapper to determine which interests to adjust.
        /// </summary>
        private Dictionary<string, double> ComputeFeedbackDeltas(string placeCategory, string feedbackType)
        {
            var deltas = new Dictionary<string, double>();

            // Map place category to interests
            var placeInterests = _categoryMapper.MapToInterests(placeCategory);

            if (!placeInterests.Any())
                return deltas; // No recognized category

            // Determine delta direction and magnitude
            var (direction, baseMagnitude) = feedbackType.ToLower() switch
            {
                "like" => (1.0, 0.05),    // Increase by 0.05
                "dislike" => (-1.0, 0.03), // Decrease by 0.03
                "skip" => (-1.0, 0.01),    // Small decrease by 0.01
                _ => (0.0, 0.0)
            };

            if (baseMagnitude == 0.0)
                return deltas;

            // Apply deltas to matching interest weights
            foreach (var (interest, placeWeight) in placeInterests)
            {
                var weightKey = $"{interest}Weight";
                var delta = direction * baseMagnitude * placeWeight; // Scale by place's relevance to interest

                deltas[weightKey] = delta;
            }

            // Contextual adjustments based on feedback type
            if (feedbackType.ToLower() == "like")
            {
                // Positive feedback can also boost related preferences
                var category = placeCategory.ToLower();

                if (category.Contains("restaurant") || category.Contains("cafe"))
                {
                    deltas["TasteQualityWeight"] = 0.02; // Small boost to quality preference
                }

                if (category.Contains("spa") || category.Contains("wellness") ||
                    category.Contains("park") || category.Contains("library"))
                {
                    deltas["CalmnessWeight"] = 0.02; // Boost calmness preference
                }

                if (category.Contains("art") || category.Contains("museum") ||
                    category.Contains("gallery") || category.Contains("design"))
                {
                    deltas["DesignWeight"] = 0.02; // Boost design preference
                }
            }

            return deltas;
        }

        /// <summary>
        /// Map entity to DTO.
        /// </summary>
        private TasteProfileDto MapToDto(UserTasteProfile profile)
        {
            return new TasteProfileDto
            {
                Id = profile.Id,
                UserId = profile.UserId,
                QuizVersion = profile.QuizVersion,
                Interests = new Dictionary<string, double>
                {
                    ["Culture"] = profile.CultureWeight,
                    ["Food"] = profile.FoodWeight,
                    ["Nature"] = profile.NatureWeight,
                    ["Nightlife"] = profile.NightlifeWeight,
                    ["Shopping"] = profile.ShoppingWeight,
                    ["Art"] = profile.ArtWeight,
                    ["Wellness"] = profile.WellnessWeight,
                    ["Sports"] = profile.SportsWeight
                },
                Preferences = new Dictionary<string, double>
                {
                    ["TasteQuality"] = profile.TasteQualityWeight,
                    ["Atmosphere"] = profile.AtmosphereWeight,
                    ["Design"] = profile.DesignWeight,
                    ["Calmness"] = profile.CalmnessWeight,
                    ["Spaciousness"] = profile.SpaciousnessWeight
                },
                NoveltyTolerance = profile.NoveltyTolerance,
                CreatedAtUtc = profile.CreatedAtUtc,
                UpdatedAtUtc = profile.UpdatedAtUtc
            };
        }
    }
}
