using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Observability
{
    /// <summary>
    /// Scoped implementation of IObservabilityContext that captures
    /// correlation IDs and user traits for consistent observability enrichment.
    /// </summary>
    public class ObservabilityContext : IObservabilityContext
    {
        private readonly Dictionary<string, string> _baggage = new();
        private Guid? _userId;
        private string? _userIdHash;

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservabilityContext"/> class.
        /// </summary>
        public ObservabilityContext()
        {
            CorrelationId = Guid.NewGuid().ToString("N");
            TraceId = Activity.Current?.TraceId.ToString();
        }

        /// <inheritdoc/>
        public string CorrelationId { get; }

        /// <inheritdoc/>
        public string? TraceId { get; }

        /// <inheritdoc/>
        public string? UserIdHash => _userIdHash;

        /// <inheritdoc/>
        public Guid? UserId => _userId;

        /// <inheritdoc/>
        public bool? IsPremium { get; set; }

        /// <inheritdoc/>
        public string? Endpoint { get; private set; }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, string> Baggage => _baggage;

        /// <inheritdoc/>
        public void SetUserId(Guid userId)
        {
            _userId = userId;
            _userIdHash = ComputeUserIdHash(userId);
        }

        /// <inheritdoc/>
        public void SetEndpoint(string endpoint)
        {
            Endpoint = endpoint;
        }

        /// <inheritdoc/>
        public void AddBaggage(string key, string value)
        {
            _baggage[key] = value;
        }

        /// <summary>
        /// Computes a SHA256 hash of the user ID, truncated to 16 characters
        /// for safe metric cardinality while maintaining uniqueness.
        /// </summary>
        /// <param name="userId">The user ID to hash</param>
        /// <returns>Truncated hash string</returns>
        private static string ComputeUserIdHash(Guid userId)
        {
            var bytes = Encoding.UTF8.GetBytes(userId.ToString());
            var hash = SHA256.HashData(bytes);
            var hashString = Convert.ToHexString(hash);
            return hashString.Substring(0, 16).ToLowerInvariant();
        }
    }
}
