using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Service for managing user taste profiles.
    /// Handles profile retrieval, updates, and feedback-driven evolution.
    /// </summary>
    public interface ITasteProfileService
    {
        /// <summary>
        /// Get user's taste profile.
        /// Returns null if user has no profile.
        /// </summary>
        Task<TasteProfileDto?> GetProfileAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get user's taste profile entity (for internal use).
        /// Returns null if user has no profile.
        /// </summary>
        Task<UserTasteProfile?> GetProfileEntityAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get profile summary for display.
        /// </summary>
        Task<TasteProfileSummaryDto> GetProfileSummaryAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Update profile manually (user edits weights).
        /// </summary>
        Task<TasteProfileDto> UpdateProfileAsync(
            Guid userId,
            Dictionary<string, double> weights,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Apply feedback delta to profile (incremental evolution).
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <param name="feedbackRequest">Feedback request with place info.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Response with updated profile summary.</returns>
        Task<PlaceFeedbackResponse> ApplyFeedbackAsync(
            Guid userId,
            PlaceFeedbackRequest feedbackRequest,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if user has a taste profile.
        /// </summary>
        Task<bool> HasProfileAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete user's taste profile.
        /// </summary>
        Task DeleteProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
