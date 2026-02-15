using System;
using System.Threading;
using System.Threading.Tasks;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Service for managing the taste quiz flow.
    /// Handles quiz retrieval, submission, and claiming of anonymous drafts.
    /// </summary>
    public interface ITasteQuizService
    {
        /// <summary>
        /// Get the current taste quiz definition with localized text.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Localized quiz definition.</returns>
        Task<TasteQuizDto> GetQuizAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Submit quiz answers.
        /// If userId is provided (authenticated), profile is persisted immediately.
        /// If userId is null (anonymous), profile is saved as draft with claim token.
        /// </summary>
        /// <param name="request">Quiz answers.</param>
        /// <param name="userId">User ID if authenticated, null if anonymous.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Response with profile summary and optional claim token.</returns>
        Task<TasteQuizSubmitResponse> SubmitQuizAsync(
            TasteQuizSubmitRequest request,
            Guid? userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Claim an anonymous quiz draft and persist it for the authenticated user.
        /// </summary>
        /// <param name="request">Claim token.</param>
        /// <param name="userId">Authenticated user ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Claimed profile or error.</returns>
        Task<TasteQuizClaimResponse> ClaimDraftAsync(
            TasteQuizClaimRequest request,
            Guid userId,
            CancellationToken cancellationToken = default);
    }
}
