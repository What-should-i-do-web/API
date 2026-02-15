using System.Collections.Generic;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.DTOs.Requests
{
    /// <summary>
    /// Request to submit completed taste quiz answers.
    /// Can be submitted by authenticated users (persists immediately)
    /// or anonymous users (creates draft with claim token).
    /// </summary>
    public class TasteQuizSubmitRequest
    {
        /// <summary>
        /// Quiz version that was completed (must match server version).
        /// </summary>
        public string QuizVersion { get; set; } = string.Empty;

        /// <summary>
        /// User's answers to quiz questions.
        /// Key = step ID, Value = selected option ID.
        /// </summary>
        public Dictionary<string, string> Answers { get; set; } = new();
    }

    /// <summary>
    /// Response after submitting taste quiz.
    /// </summary>
    public class TasteQuizSubmitResponse
    {
        /// <summary>
        /// Profile state: "complete" (authenticated) or "draft" (anonymous).
        /// </summary>
        public string ProfileState { get; set; } = string.Empty;

        /// <summary>
        /// Computed taste profile summary.
        /// </summary>
        public TasteProfileSummaryDto Profile { get; set; } = new();

        /// <summary>
        /// Claim token for anonymous users to claim their profile later.
        /// Null for authenticated users (profile already persisted).
        /// </summary>
        public string? ClaimToken { get; set; }

        /// <summary>
        /// Claim token expiration time (UTC) for anonymous users.
        /// </summary>
        public DateTime? ClaimTokenExpiresAt { get; set; }
    }
}
