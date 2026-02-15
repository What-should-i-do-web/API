using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using WhatShouldIDo.Application.Configuration;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Infrastructure.Services
{
    /// <summary>
    /// Orchestrates taste quiz operations including submission, draft management, and profile computation.
    /// </summary>
    public class TasteQuizService : ITasteQuizService
    {
        private readonly TasteQuizOptions _quizOptions;
        private readonly ITasteProfileRepository _profileRepository;
        private readonly ITasteEventRepository _eventRepository;
        private readonly ITasteDraftStore _draftStore;
        private readonly IClock _clock;

        public TasteQuizService(
            IOptions<TasteQuizOptions> quizOptions,
            ITasteProfileRepository profileRepository,
            ITasteEventRepository eventRepository,
            ITasteDraftStore draftStore,
            IClock clock)
        {
            _quizOptions = quizOptions.Value;
            _profileRepository = profileRepository;
            _eventRepository = eventRepository;
            _draftStore = draftStore;
            _clock = clock;
        }

        public Task<TasteQuizDto> GetQuizAsync(CancellationToken cancellationToken = default)
        {
            // Return server-driven quiz definition
            var quiz = new TasteQuizDto
            {
                Version = _quizOptions.Version,
                Steps = _quizOptions.Steps.Select(step => new TasteQuizStepDto
                {
                    Id = step.Id,
                    Type = step.Type,
                    Title = step.TitleKey,
                    Description = step.DescriptionKey,
                    Options = step.Options.Select(opt => new TasteQuizOptionDto
                    {
                        Id = opt.Id,
                        Label = opt.LabelKey,
                        ImageUrl = opt.ImageUrl
                    }).ToList()
                }).ToList()
            };

            return Task.FromResult(quiz);
        }

        public async Task<TasteQuizSubmitResponse> SubmitQuizAsync(
            TasteQuizSubmitRequest request,
            Guid? userId,
            CancellationToken cancellationToken = default)
        {
            // Extract data from request
            var quizVersion = request.QuizVersion;
            var answers = request.Answers;

            // Compute profile weights from answers
            var weights = ComputeWeightsFromAnswers(answers);

            var now = _clock.UtcNow;

            // Create profile
            var profile = UserTasteProfile.CreateFromQuiz(
                userId ?? Guid.Empty, // Empty for anonymous
                quizVersion,
                weights,
                now
            );

            // If authenticated, save to database
            if (userId.HasValue)
            {
                // Check if profile already exists
                var existing = await _profileRepository.GetByUserIdAsync(userId.Value, cancellationToken);

                UserTasteProfile finalProfile;

                if (existing != null)
                {
                    // Update existing profile with new quiz
                    existing.UpdateFromQuiz(quizVersion, weights, now);
                    await _profileRepository.UpdateAsync(existing, cancellationToken);

                    // Record event
                    var updateEvent = UserTasteEvent.QuizCompleted(
                        userId.Value,
                        quizVersion,
                        answers,
                        now
                    );
                    await _eventRepository.AddAsync(updateEvent, cancellationToken);

                    finalProfile = existing;
                }
                else
                {
                    // Create new profile
                    await _profileRepository.CreateAsync(profile, cancellationToken);

                    // Record event
                    var createEvent = UserTasteEvent.QuizCompleted(
                        userId.Value,
                        quizVersion,
                        answers,
                        now
                    );
                    await _eventRepository.AddAsync(createEvent, cancellationToken);

                    finalProfile = profile;
                }

                // Return response for authenticated user
                return new TasteQuizSubmitResponse
                {
                    ProfileState = "complete",
                    Profile = MapToSummary(finalProfile),
                    ClaimToken = null,
                    ClaimTokenExpiresAt = null
                };
            }
            else
            {
                // Anonymous user - save draft to Redis
                var claimToken = GenerateClaimToken();
                var ttl = TimeSpan.FromHours(_quizOptions.DraftTtlHours);
                var expiresAt = now.Add(ttl);

                await _draftStore.SaveDraftAsync(claimToken, profile, ttl, cancellationToken);

                // Return response for anonymous user
                return new TasteQuizSubmitResponse
                {
                    ProfileState = "draft",
                    Profile = MapToSummary(profile),
                    ClaimToken = claimToken,
                    ClaimTokenExpiresAt = expiresAt
                };
            }
        }

        public async Task<TasteQuizClaimResponse> ClaimDraftAsync(
            TasteQuizClaimRequest request,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            // Extract claim token from request
            var claimToken = request.ClaimToken;

            // Get draft from Redis
            var draft = await _draftStore.GetDraftAsync(claimToken, cancellationToken);

            if (draft == null)
            {
                // Draft expired or invalid token
                return new TasteQuizClaimResponse
                {
                    Success = false,
                    Profile = null,
                    ErrorMessage = "Invalid or expired claim token"
                };
            }

            // Check if user already has a profile
            var existing = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);

            UserTasteProfile finalProfile;

            if (existing != null)
            {
                // User already has a profile - merge draft into existing
                var weights = draft.GetAllWeights();
                existing.UpdateFromQuiz(draft.QuizVersion, weights, _clock.UtcNow);

                await _profileRepository.UpdateAsync(existing, cancellationToken);

                // Record claim event
                var claimEvent = UserTasteEvent.CreateProfileClaimed(
                    userId,
                    claimToken,
                    _clock.UtcNow
                );
                await _eventRepository.AddAsync(claimEvent, cancellationToken);

                // Delete draft
                await _draftStore.DeleteDraftAsync(claimToken, cancellationToken);

                finalProfile = existing;
            }
            else
            {
                // Assign draft to user
                var weights = draft.GetAllWeights();
                var newProfile = UserTasteProfile.CreateFromQuiz(
                    userId,
                    draft.QuizVersion,
                    weights,
                    _clock.UtcNow
                );

                await _profileRepository.CreateAsync(newProfile, cancellationToken);

                // Record claim event
                var claimEvent = UserTasteEvent.CreateProfileClaimed(
                    userId,
                    claimToken,
                    _clock.UtcNow
                );
                await _eventRepository.AddAsync(claimEvent, cancellationToken);

                // Delete draft
                await _draftStore.DeleteDraftAsync(claimToken, cancellationToken);

                finalProfile = newProfile;
            }

            // Return success response
            return new TasteQuizClaimResponse
            {
                Success = true,
                Profile = MapToDto(finalProfile),
                ErrorMessage = null
            };
        }

        /// <summary>
        /// Compute profile weights from quiz answers.
        /// Maps answer options to weight adjustments.
        /// </summary>
        private Dictionary<string, double> ComputeWeightsFromAnswers(Dictionary<string, string> answers)
        {
            // Start with neutral weights (0.5 for all dimensions)
            var weights = new Dictionary<string, double>
            {
                ["CultureWeight"] = 0.5,
                ["FoodWeight"] = 0.5,
                ["NatureWeight"] = 0.5,
                ["NightlifeWeight"] = 0.5,
                ["ShoppingWeight"] = 0.5,
                ["ArtWeight"] = 0.5,
                ["WellnessWeight"] = 0.5,
                ["SportsWeight"] = 0.5,
                ["TasteQualityWeight"] = 0.5,
                ["AtmosphereWeight"] = 0.5,
                ["DesignWeight"] = 0.5,
                ["CalmnessWeight"] = 0.5,
                ["SpaciousnessWeight"] = 0.5,
                ["NoveltyTolerance"] = 0.5
            };

            // Apply answer-based adjustments
            foreach (var (stepId, optionId) in answers)
            {
                // Find the step definition
                var step = _quizOptions.Steps.FirstOrDefault(s => s.Id == stepId);
                if (step == null) continue;

                // Find the option definition
                var option = step.Options.FirstOrDefault(o => o.Id == optionId);
                if (option?.Deltas == null) continue;

                // Apply weight adjustments
                foreach (var kvp in option.Deltas)
                {
                    var weightKey = kvp.Key;
                    var delta = kvp.Value;

                    if (weights.ContainsKey(weightKey))
                    {
                        weights[weightKey] = Math.Max(0.0, Math.Min(1.0, weights[weightKey] + delta));
                    }
                }
            }

            return weights;
        }

        /// <summary>
        /// Generate secure random claim token (256-bit).
        /// </summary>
        private string GenerateClaimToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32); // 256 bits
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('='); // URL-safe Base64
        }

        /// <summary>
        /// Map entity to summary DTO.
        /// </summary>
        private TasteProfileSummaryDto MapToSummary(UserTasteProfile profile)
        {
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

        /// <summary>
        /// Map entity to full DTO.
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
