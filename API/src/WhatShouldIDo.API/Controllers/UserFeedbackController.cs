using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.Interfaces;
using System.Security.Claims;

namespace WhatShouldIDo.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserFeedbackController : ControllerBase
    {
        private readonly IVisitTrackingService _visitTrackingService;
        private readonly IPreferenceLearningService _preferenceLearningService;
        private readonly ILogger<UserFeedbackController> _logger;

        public UserFeedbackController(
            IVisitTrackingService visitTrackingService,
            IPreferenceLearningService preferenceLearningService,
            ILogger<UserFeedbackController> logger)
        {
            _visitTrackingService = visitTrackingService;
            _preferenceLearningService = preferenceLearningService;
            _logger = logger;
        }

        [HttpPost("rate")]
        public async Task<IActionResult> RatePlace([FromBody] UserFeedbackRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                // Log the feedback
                await _visitTrackingService.LogUserFeedbackAsync(
                    userId.Value, 
                    request.PlaceId, 
                    request.Rating, 
                    request.Review,
                    request.WouldRecommend, 
                    cancellationToken);

                // If user confirms visit, log that too
                if (request.ConfirmVisit)
                {
                    await _visitTrackingService.LogVisitConfirmationAsync(
                        userId.Value, 
                        request.PlaceId, 
                        request.VisitDurationMinutes,
                        cancellationToken);
                }

                // Update user preferences based on feedback
                await _preferenceLearningService.UpdateUserPreferencesAsync(userId.Value, cancellationToken);

                return Ok(new { message = "Feedback received successfully", rating = request.Rating });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing user feedback");
                return StatusCode(500, new { error = "Failed to process feedback" });
            }
        }

        [HttpPost("confirm-visit")]
        public async Task<IActionResult> ConfirmVisit([FromBody] VisitConfirmationRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                await _visitTrackingService.LogVisitConfirmationAsync(
                    userId.Value, 
                    request.PlaceId, 
                    request.DurationMinutes,
                    cancellationToken);

                // Update preferences after confirmed visit
                await _preferenceLearningService.UpdateUserPreferencesAsync(userId.Value, cancellationToken);

                return Ok(new { message = "Visit confirmed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming visit");
                return StatusCode(500, new { error = "Failed to confirm visit" });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetVisitHistory([FromQuery] int days = 30, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var visits = await _visitTrackingService.GetUserVisitHistoryAsync(userId.Value, days, cancellationToken);
                
                var response = visits.Select(v => new
                {
                    id = v.Id,
                    placeName = v.PlaceName,
                    visitDate = v.VisitDate,
                    confirmed = v.VisitConfirmed,
                    rating = v.UserRating,
                    review = v.UserReview,
                    wouldRecommend = v.WouldRecommend,
                    durationMinutes = v.DurationMinutes,
                    reason = v.OriginalSuggestionReason
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving visit history");
                return StatusCode(500, new { error = "Failed to retrieve visit history" });
            }
        }

        [HttpGet("preferences")]
        public async Task<IActionResult> GetLearnedPreferences(CancellationToken cancellationToken)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var preferences = await _preferenceLearningService.GetLearnedPreferencesAsync(userId.Value, cancellationToken);
                var personalizationScore = await _preferenceLearningService.CalculatePersonalizationScoreAsync(userId.Value, cancellationToken);

                var response = new
                {
                    favoriteCuisines = preferences.FavoriteCuisines,
                    favoriteActivities = preferences.FavoriteActivityTypes,
                    avoidedActivities = preferences.AvoidedActivityTypes,
                    timePreferences = preferences.TimePreferences,
                    budgetRange = preferences.PreferredBudgetRange,
                    preferredRadius = preferences.PreferredRadius,
                    personalizationScore = personalizationScore,
                    confidence = preferences.PersonalizationConfidence,
                    lastUpdated = preferences.LastUpdated
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving learned preferences");
                return StatusCode(500, new { error = "Failed to retrieve preferences" });
            }
        }

        [HttpGet("recommendations")]
        public async Task<IActionResult> GetPersonalizedRecommendations(CancellationToken cancellationToken)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var favoriteCuisines = await _preferenceLearningService.GetRecommendedCuisinesAsync(userId.Value, cancellationToken);
                var favoriteActivities = await _preferenceLearningService.GetRecommendedActivitiesAsync(userId.Value, cancellationToken);
                var optimalTime = await _preferenceLearningService.GetOptimalTimePreferenceAsync(userId.Value, cancellationToken);

                var response = new
                {
                    recommendedCuisines = favoriteCuisines,
                    recommendedActivities = favoriteActivities,
                    optimalTime = optimalTime,
                    tips = GeneratePersonalizedTips(favoriteCuisines, favoriteActivities, optimalTime)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating personalized recommendations");
                return StatusCode(500, new { error = "Failed to generate recommendations" });
            }
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                             User.FindFirst("sub")?.Value;
            
            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        private static List<string> GeneratePersonalizedTips(List<string> cuisines, List<string> activities, string optimalTime)
        {
            var tips = new List<string>();

            if (cuisines.Any())
            {
                tips.Add($"Size {cuisines.First()} mutfağı öneriyoruz - favorilerinizde!");
            }

            if (activities.Any())
            {
                tips.Add($"{activities.First()} aktivitelerine ilginiz var - yeni seçenekleri keşfedin");
            }

            tips.Add($"{optimalTime} saatleri size en uygun görünüyor");

            tips.Add("Daha iyi öneriler için ziyaret ettiğiniz yerleri puanlayın");

            return tips;
        }
    }
}