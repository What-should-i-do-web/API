using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Service for managing user subscriptions
    /// </summary>
    public interface ISubscriptionService
    {
        /// <summary>
        /// Gets the current subscription for a user
        /// </summary>
        /// <param name="userId">The user's ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The user's subscription DTO</returns>
        Task<SubscriptionDto> GetMySubscriptionAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a user currently has premium entitlement
        /// </summary>
        /// <param name="userId">The user's ID</param>
        /// <param name="utcNow">The current UTC time</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if user has premium entitlement</returns>
        Task<bool> UserHasPremiumEntitlementAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies a receipt and updates the user's subscription
        /// </summary>
        /// <param name="userId">The user's ID</param>
        /// <param name="request">The verification request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The verification result</returns>
        Task<VerifySubscriptionResultDto> VerifyReceiptAsync(
            Guid userId,
            VerifyReceiptRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Ensures a subscription record exists for a user (creates default if missing)
        /// </summary>
        /// <param name="userId">The user's ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task EnsureSubscriptionExistsAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Manually grants a subscription to a user (admin-only operation).
        /// Uses Provider=Manual and records notes for audit trail.
        /// </summary>
        /// <param name="request">The manual grant request</param>
        /// <param name="adminUserId">The admin user performing the grant (for audit)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The updated subscription DTO</returns>
        Task<SubscriptionDto> ManualGrantAsync(
            ManualGrantRequest request,
            Guid adminUserId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Revokes a manual grant, resetting the user to free tier (admin-only operation).
        /// </summary>
        /// <param name="userId">The user whose grant to revoke</param>
        /// <param name="adminUserId">The admin user performing the revocation (for audit)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if revoked, false if user had no manual grant</returns>
        Task<bool> RevokeManualGrantAsync(
            Guid userId,
            Guid adminUserId,
            CancellationToken cancellationToken = default);
    }
}
