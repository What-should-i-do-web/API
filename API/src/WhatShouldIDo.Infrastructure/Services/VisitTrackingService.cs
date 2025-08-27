using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Data;
using System.Text.Json;

namespace WhatShouldIDo.Infrastructure.Services
{
    public class VisitTrackingService : IVisitTrackingService
    {
        private readonly WhatShouldIDoDbContext _context;
        private readonly ILogger<VisitTrackingService> _logger;

        public VisitTrackingService(WhatShouldIDoDbContext context, ILogger<VisitTrackingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogSuggestionViewAsync(Guid userId, Place place, string reason, CancellationToken cancellationToken = default)
        {
            try
            {
                var visit = new UserVisit
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PlaceId = place.Id,
                    PlaceName = place.Name,
                    Latitude = place.Latitude,
                    Longitude = place.Longitude,
                    VisitDate = DateTime.UtcNow,
                    OriginalSuggestionReason = reason,
                    Source = "app",
                    TimeOfDay = GetTimeOfDay(DateTime.Now),
                    DayOfWeek = DateTime.Now.DayOfWeek.ToString().ToLower(),
                    WeatherCondition = "unknown", // TODO: Integrate weather API
                    VisitConfirmed = false
                };

                _context.UserVisits.Add(visit);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Logged suggestion view: User {UserId} viewed {PlaceName}", userId, place.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging suggestion view for user {UserId}", userId);
            }
        }

        public async Task LogVisitConfirmationAsync(Guid userId, Guid placeId, int? durationMinutes = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var visit = await _context.UserVisits
                    .Where(v => v.UserId == userId && v.PlaceId == placeId)
                    .OrderByDescending(v => v.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (visit != null)
                {
                    visit.VisitConfirmed = true;
                    visit.ConfirmationDate = DateTime.UtcNow;
                    visit.DurationMinutes = durationMinutes;
                    
                    await _context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Visit confirmed: User {UserId} confirmed visit to {PlaceName}", userId, visit.PlaceName);
                }
                else
                {
                    // Create new confirmed visit if no previous record
                    var confirmedVisit = new UserVisit
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        PlaceId = placeId,
                        PlaceName = "Unknown Place",
                        VisitDate = DateTime.UtcNow,
                        VisitConfirmed = true,
                        ConfirmationDate = DateTime.UtcNow,
                        DurationMinutes = durationMinutes,
                        Source = "user_confirmed",
                        TimeOfDay = GetTimeOfDay(DateTime.Now),
                        DayOfWeek = DateTime.Now.DayOfWeek.ToString().ToLower()
                    };

                    _context.UserVisits.Add(confirmedVisit);
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming visit for user {UserId}", userId);
            }
        }

        public async Task LogUserFeedbackAsync(Guid userId, Guid placeId, float rating, string? review = null, bool wouldRecommend = true, CancellationToken cancellationToken = default)
        {
            try
            {
                var visit = await _context.UserVisits
                    .Where(v => v.UserId == userId && v.PlaceId == placeId)
                    .OrderByDescending(v => v.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (visit != null)
                {
                    visit.UserRating = rating;
                    visit.UserReview = review;
                    visit.WouldRecommend = wouldRecommend;
                    visit.WouldVisitAgain = rating >= 3.5f; // Auto-calculate based on rating
                    
                    await _context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("User feedback logged: User {UserId} rated {PlaceName} with {Rating} stars", 
                        userId, visit.PlaceName, rating);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging user feedback for user {UserId}", userId);
            }
        }

        public async Task<List<UserVisit>> GetUserVisitHistoryAsync(Guid userId, int days = 30, CancellationToken cancellationToken = default)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-days);
                
                return await _context.UserVisits
                    .AsNoTracking()
                    .Where(v => v.UserId == userId && v.VisitDate >= cutoffDate)
                    .OrderByDescending(v => v.VisitDate)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving visit history for user {UserId}", userId);
                return new List<UserVisit>();
            }
        }

        public async Task<List<Place>> GetRecentlyVisitedPlacesAsync(Guid userId, int days = 30, CancellationToken cancellationToken = default)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-days);
                
                var visitedPlaceIds = await _context.UserVisits
                    .AsNoTracking()
                    .Where(v => v.UserId == userId && v.VisitDate >= cutoffDate && v.VisitConfirmed)
                    .Select(v => v.PlaceId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                return await _context.Places
                    .AsNoTracking()
                    .Where(p => visitedPlaceIds.Contains(p.Id))
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recently visited places for user {UserId}", userId);
                return new List<Place>();
            }
        }

        public async Task<bool> HasUserVisitedPlaceAsync(Guid userId, Guid placeId, int days = 30, CancellationToken cancellationToken = default)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-days);
                
                return await _context.UserVisits
                    .AsNoTracking()
                    .AnyAsync(v => v.UserId == userId && v.PlaceId == placeId && v.VisitDate >= cutoffDate, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user visited place: {UserId}", userId);
                return false;
            }
        }

        public async Task<Dictionary<string, int>> GetUserCategoryPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var visits = await _context.UserVisits
                    .AsNoTracking()
                    .Where(v => v.UserId == userId && v.VisitConfirmed && v.UserRating >= 3.0f)
                    .ToListAsync(cancellationToken);

                var categoryPreferences = new Dictionary<string, int>();

                foreach (var visit in visits)
                {
                    // Get place details to extract categories
                    var place = await _context.Places
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == visit.PlaceId, cancellationToken);

                    if (place?.Category != null)
                    {
                        var categories = place.Category.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var category in categories)
                        {
                            var cleanCategory = category.Trim().ToLower();
                            categoryPreferences[cleanCategory] = categoryPreferences.GetValueOrDefault(cleanCategory, 0) + 1;
                        }
                    }
                }

                return categoryPreferences.OrderByDescending(kvp => kvp.Value).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating category preferences for user {UserId}", userId);
                return new Dictionary<string, int>();
            }
        }

        public async Task<float> GetPlaceAvoidanceScoreAsync(Guid userId, Place place, CancellationToken cancellationToken = default)
        {
            try
            {
                var avoidanceScore = 0f;

                // Check if user has visited this exact place recently
                var recentVisit = await _context.UserVisits
                    .AsNoTracking()
                    .Where(v => v.UserId == userId && v.PlaceId == place.Id)
                    .OrderByDescending(v => v.VisitDate)
                    .FirstOrDefaultAsync(cancellationToken);

                if (recentVisit != null)
                {
                    var daysSinceVisit = (DateTime.UtcNow - recentVisit.VisitDate).TotalDays;
                    
                    // Strong avoidance for recent visits
                    if (daysSinceVisit < 7) avoidanceScore += 1.0f;
                    else if (daysSinceVisit < 30) avoidanceScore += 0.5f;
                    
                    // Consider user rating
                    if (recentVisit.UserRating.HasValue && recentVisit.UserRating < 3.0f)
                        avoidanceScore += 0.8f; // Avoid poorly rated places
                }

                // Check for similar places nearby (within 100m)
                var nearbyVisits = await _context.UserVisits
                    .AsNoTracking()
                    .Where(v => v.UserId == userId && v.VisitDate >= DateTime.UtcNow.AddDays(-14))
                    .ToListAsync(cancellationToken);

                foreach (var visit in nearbyVisits)
                {
                    var distance = CalculateDistance(place.Latitude, place.Longitude, visit.Latitude, visit.Longitude);
                    if (distance < 100) // Within 100 meters
                    {
                        avoidanceScore += 0.3f;
                    }
                }

                return Math.Min(avoidanceScore, 2.0f); // Cap at 2.0
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating avoidance score for user {UserId}", userId);
                return 0f;
            }
        }

        private static string GetTimeOfDay(DateTime dateTime)
        {
            var hour = dateTime.Hour;
            return hour switch
            {
                >= 5 and < 12 => "morning",
                >= 12 and < 17 => "afternoon", 
                >= 17 and < 22 => "evening",
                _ => "night"
            };
        }

        private static double CalculateDistance(float lat1, float lon1, float lat2, float lon2)
        {
            const double R = 6371e3; // Earth's radius in meters
            var φ1 = lat1 * Math.PI / 180;
            var φ2 = lat2 * Math.PI / 180;
            var Δφ = (lat2 - lat1) * Math.PI / 180;
            var Δλ = (lon2 - lon1) * Math.PI / 180;

            var a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                    Math.Cos(φ1) * Math.Cos(φ2) *
                    Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }
    }
}