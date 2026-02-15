using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.DTOs.Requests
{
    /// <summary>
    /// Request to claim a taste profile from an anonymous quiz draft.
    /// Requires authentication.
    /// </summary>
    public class TasteQuizClaimRequest
    {
        /// <summary>
        /// Claim token received when submitting quiz anonymously.
        /// </summary>
        public string ClaimToken { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response after claiming taste profile.
    /// </summary>
    public class TasteQuizClaimResponse
    {
        /// <summary>
        /// Success indicator.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Claimed and persisted taste profile.
        /// </summary>
        public TasteProfileDto? Profile { get; set; }

        /// <summary>
        /// Error message if claim failed (e.g., "Invalid or expired token").
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
