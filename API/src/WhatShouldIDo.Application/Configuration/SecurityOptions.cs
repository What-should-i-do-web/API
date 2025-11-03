using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Application.Configuration
{
    /// <summary>
    /// Configuration options for security (JWT, rate limiting, webhooks).
    /// </summary>
    public class SecurityOptions
    {
        /// <summary>
        /// Gets or sets JWT validation options.
        /// </summary>
        [Required]
        public JwtOptions Jwt { get; set; } = new();

        /// <summary>
        /// Gets or sets rate limiting options.
        /// </summary>
        [Required]
        public RateLimitOptions RateLimit { get; set; } = new();

        /// <summary>
        /// Gets or sets webhook security options.
        /// </summary>
        [Required]
        public WebhookOptions Webhook { get; set; } = new();

        /// <summary>
        /// Gets or sets Redis security options.
        /// </summary>
        [Required]
        public RedisSecurityOptions Redis { get; set; } = new();
    }

    /// <summary>
    /// JWT validation configuration.
    /// </summary>
    public class JwtOptions
    {
        /// <summary>
        /// Gets or sets the valid issuer(s) for JWT tokens.
        /// </summary>
        [Required]
        [MinLength(1)]
        public string ValidIssuer { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the valid audience(s) for JWT tokens.
        /// </summary>
        [Required]
        [MinLength(1)]
        public string ValidAudience { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether to validate the token signature.
        /// Default: true (MUST be true in production)
        /// </summary>
        public bool ValidateSignature { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate the token issuer.
        /// Default: true
        /// </summary>
        public bool ValidateIssuer { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate the token audience.
        /// Default: true
        /// </summary>
        public bool ValidateAudience { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate the token lifetime.
        /// Default: true
        /// </summary>
        public bool ValidateLifetime { get; set; } = true;

        /// <summary>
        /// Gets or sets the clock skew tolerance for token expiration (in seconds).
        /// Default: 300 (5 minutes)
        /// </summary>
        [Range(0, 600)]
        public int ClockSkewSeconds { get; set; } = 300;

        /// <summary>
        /// Gets or sets the maximum token lifetime in seconds.
        /// Default: 3600 (1 hour)
        /// </summary>
        [Range(300, 86400)]
        public int MaxTokenLifetimeSeconds { get; set; } = 3600;
    }

    /// <summary>
    /// Rate limiting configuration.
    /// </summary>
    public class RateLimitOptions
    {
        /// <summary>
        /// Gets or sets whether rate limiting is enabled.
        /// Default: true
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the default rate limit window in seconds.
        /// Default: 60 (1 minute)
        /// </summary>
        [Range(1, 3600)]
        public int WindowSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets the default maximum requests per window for authenticated users.
        /// Default: 100
        /// </summary>
        [Range(1, 10000)]
        public int MaxRequestsPerWindow { get; set; } = 100;

        /// <summary>
        /// Gets or sets the default maximum requests per window for anonymous users.
        /// Default: 20
        /// </summary>
        [Range(1, 1000)]
        public int MaxRequestsPerWindowAnonymous { get; set; } = 20;

        /// <summary>
        /// Gets or sets whether premium users bypass rate limiting.
        /// Default: false
        /// </summary>
        public bool PremiumBypass { get; set; } = false;

        /// <summary>
        /// Gets or sets the HTTP status code to return when rate limit is exceeded.
        /// Default: 429
        /// </summary>
        public int StatusCode { get; set; } = 429;
    }

    /// <summary>
    /// Webhook security configuration.
    /// </summary>
    public class WebhookOptions
    {
        /// <summary>
        /// Gets or sets whether webhook signature verification is enabled.
        /// Default: true (MUST be true in production)
        /// </summary>
        public bool VerifySignature { get; set; } = true;

        /// <summary>
        /// Gets or sets the webhook signing secret.
        /// Should be loaded from secure configuration (KeyVault, etc.)
        /// </summary>
        [Required]
        [MinLength(32)]
        public string SigningSecret { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the webhook signature header name.
        /// Default: "X-Webhook-Signature"
        /// </summary>
        [Required]
        public string SignatureHeader { get; set; } = "X-Webhook-Signature";

        /// <summary>
        /// Gets or sets the webhook timestamp header name for replay protection.
        /// Default: "X-Webhook-Timestamp"
        /// </summary>
        [Required]
        public string TimestampHeader { get; set; } = "X-Webhook-Timestamp";

        /// <summary>
        /// Gets or sets the maximum webhook timestamp age in seconds (replay protection).
        /// Default: 300 (5 minutes)
        /// </summary>
        [Range(60, 3600)]
        public int MaxTimestampAgeSeconds { get; set; } = 300;

        /// <summary>
        /// Gets or sets whether to use idempotency keys for webhook events.
        /// Default: true
        /// </summary>
        public bool UseIdempotencyKeys { get; set; } = true;
    }

    /// <summary>
    /// Redis security configuration.
    /// </summary>
    public class RedisSecurityOptions
    {
        /// <summary>
        /// Gets or sets whether to use TLS for Redis connections.
        /// Default: true (MUST be true in production)
        /// </summary>
        public bool UseTls { get; set; } = true;

        /// <summary>
        /// Gets or sets the Redis ACL username.
        /// </summary>
        public string? AclUsername { get; set; }

        /// <summary>
        /// Gets or sets whether to use certificate validation for TLS.
        /// Default: true
        /// </summary>
        public bool ValidateCertificate { get; set; } = true;

        /// <summary>
        /// Gets or sets the minimum TLS version.
        /// Default: "1.2"
        /// </summary>
        public string MinTlsVersion { get; set; } = "1.2";
    }
}
