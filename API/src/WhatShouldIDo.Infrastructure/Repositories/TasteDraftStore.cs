using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Infrastructure.Repositories
{
    /// <summary>
    /// Redis-backed store for anonymous taste quiz drafts.
    /// Uses SHA256 hashing of claim tokens for security.
    /// </summary>
    public class TasteDraftStore : ITasteDraftStore
    {
        private readonly ICacheService _cacheService;
        private const string KeyPrefix = "taste:draft:";

        public TasteDraftStore(ICacheService cacheService)
        {
            _cacheService = cacheService;
        }

        public async Task SaveDraftAsync(
            string claimToken,
            UserTasteProfile profile,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            var key = BuildKey(claimToken);

            // Serialize profile to JSON
            var draftData = new TasteDraftData
            {
                UserId = profile.UserId,
                QuizVersion = profile.QuizVersion,
                Interests = profile.GetInterestWeights(),
                Preferences = profile.GetPreferenceWeights(),
                NoveltyTolerance = profile.NoveltyTolerance,
                CreatedAtUtc = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(draftData);

            await _cacheService.SetAsync(key, json, ttl);
        }

        public async Task<UserTasteProfile?> GetDraftAsync(
            string claimToken,
            CancellationToken cancellationToken = default)
        {
            var key = BuildKey(claimToken);

            var json = await _cacheService.GetAsync<string>(key);

            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                var draftData = JsonSerializer.Deserialize<TasteDraftData>(json);
                if (draftData == null)
                    return null;

                // Reconstruct UserTasteProfile from draft data
                var weights = new Dictionary<string, double>(draftData.Interests);
                foreach (var pref in draftData.Preferences)
                {
                    weights[pref.Key] = pref.Value;
                }
                weights["NoveltyTolerance"] = draftData.NoveltyTolerance;

                var profile = UserTasteProfile.CreateFromQuiz(
                    draftData.UserId,
                    draftData.QuizVersion,
                    weights,
                    draftData.CreatedAtUtc
                );

                return profile;
            }
            catch (JsonException)
            {
                // Invalid JSON, treat as expired
                return null;
            }
        }

        public async Task DeleteDraftAsync(
            string claimToken,
            CancellationToken cancellationToken = default)
        {
            var key = BuildKey(claimToken);
            await _cacheService.RemoveAsync(key);
        }

        public async Task<bool> ExistsAsync(
            string claimToken,
            CancellationToken cancellationToken = default)
        {
            var key = BuildKey(claimToken);
            var value = await _cacheService.GetAsync<string>(key);
            return value != null;
        }

        /// <summary>
        /// Build cache key from claim token using SHA256 hash.
        /// Never store the raw token.
        /// </summary>
        private string BuildKey(string claimToken)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(claimToken));
            var hashString = Convert.ToHexString(hash).ToLowerInvariant();
            return $"{KeyPrefix}{hashString}";
        }

        /// <summary>
        /// Internal DTO for storing draft data in Redis.
        /// </summary>
        private class TasteDraftData
        {
            public Guid UserId { get; set; }
            public string QuizVersion { get; set; } = string.Empty;
            public Dictionary<string, double> Interests { get; set; } = new();
            public Dictionary<string, double> Preferences { get; set; } = new();
            public double NoveltyTolerance { get; set; }
            public DateTime CreatedAtUtc { get; set; }
        }
    }
}
