using WhatShouldIDo.Domain.Exceptions;

namespace WhatShouldIDo.Domain.Entities
{
    /// <summary>
    /// Represents a shareable token for a route that allows read-only access.
    /// </summary>
    public class RouteShareToken : EntityBase
    {
        /// <summary>
        /// The route this token provides access to
        /// </summary>
        public Guid RouteId { get; private set; }

        /// <summary>
        /// The unique share token string (URL-safe)
        /// </summary>
        public string Token { get; private set; }

        /// <summary>
        /// When the token was created
        /// </summary>
        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// Optional expiration date (null = never expires)
        /// </summary>
        public DateTime? ExpiresAt { get; private set; }

        /// <summary>
        /// User who created the share token
        /// </summary>
        public Guid CreatedByUserId { get; private set; }

        /// <summary>
        /// Whether the token is currently active
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Number of times this share link has been accessed
        /// </summary>
        public int AccessCount { get; private set; }

        /// <summary>
        /// Last time the shared route was accessed
        /// </summary>
        public DateTime? LastAccessedAt { get; private set; }

        /// <summary>
        /// Navigation property to the route
        /// </summary>
        public Route? Route { get; private set; }

        private RouteShareToken() { }

        public RouteShareToken(Guid routeId, Guid createdByUserId, DateTime? expiresAt = null)
        {
            if (routeId == Guid.Empty)
                throw new DomainException("RouteId is required.");
            if (createdByUserId == Guid.Empty)
                throw new DomainException("CreatedByUserId is required.");

            Id = Guid.NewGuid();
            RouteId = routeId;
            CreatedByUserId = createdByUserId;
            Token = GenerateSecureToken();
            CreatedAt = DateTime.UtcNow;
            ExpiresAt = expiresAt;
            IsActive = true;
            AccessCount = 0;
        }

        /// <summary>
        /// Generates a URL-safe random token
        /// </summary>
        private static string GenerateSecureToken()
        {
            // Generate 16 random bytes (128 bits) and encode as URL-safe base64
            var randomBytes = new byte[16];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);

            // Convert to URL-safe base64 (no padding, replace +/ with -_)
            return Convert.ToBase64String(randomBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        /// <summary>
        /// Records an access to this shared route
        /// </summary>
        public void RecordAccess()
        {
            AccessCount++;
            LastAccessedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Deactivates the share token
        /// </summary>
        public void Deactivate()
        {
            IsActive = false;
        }

        /// <summary>
        /// Reactivates a previously deactivated token
        /// </summary>
        public void Reactivate()
        {
            IsActive = true;
        }

        /// <summary>
        /// Checks if the token is valid (active and not expired)
        /// </summary>
        public bool IsValid()
        {
            if (!IsActive)
                return false;

            if (ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow)
                return false;

            return true;
        }

        /// <summary>
        /// Updates the expiration date
        /// </summary>
        public void SetExpiration(DateTime? expiresAt)
        {
            ExpiresAt = expiresAt;
        }
    }
}
