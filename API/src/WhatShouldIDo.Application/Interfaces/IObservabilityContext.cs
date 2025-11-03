using System;
using System.Collections.Generic;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Provides correlation IDs and user traits for consistent span/log enrichment
    /// across the application. Scoped per request.
    /// </summary>
    public interface IObservabilityContext
    {
        /// <summary>
        /// Gets the correlation ID for the current request.
        /// Propagated to all logs, traces, and responses.
        /// </summary>
        string CorrelationId { get; }

        /// <summary>
        /// Gets the W3C trace ID for distributed tracing.
        /// </summary>
        string? TraceId { get; }

        /// <summary>
        /// Gets the hashed user ID (SHA256, truncated to 16 chars) for safe metric cardinality.
        /// Null if user is not authenticated.
        /// </summary>
        string? UserIdHash { get; }

        /// <summary>
        /// Gets the raw user ID (use sparingly, prefer UserIdHash for metrics).
        /// Null if user is not authenticated.
        /// </summary>
        Guid? UserId { get; }

        /// <summary>
        /// Gets whether the current user is premium.
        /// Null if not yet determined.
        /// </summary>
        bool? IsPremium { get; set; }

        /// <summary>
        /// Gets the endpoint being accessed (controller/action).
        /// </summary>
        string? Endpoint { get; }

        /// <summary>
        /// Gets additional baggage items for trace context propagation.
        /// </summary>
        IReadOnlyDictionary<string, string> Baggage { get; }

        /// <summary>
        /// Sets the user ID for the current context.
        /// Automatically computes UserIdHash.
        /// </summary>
        /// <param name="userId">The user ID to set</param>
        void SetUserId(Guid userId);

        /// <summary>
        /// Sets the endpoint for the current context.
        /// </summary>
        /// <param name="endpoint">The endpoint name (e.g., "PromptController.Generate")</param>
        void SetEndpoint(string endpoint);

        /// <summary>
        /// Adds a baggage item for trace context propagation.
        /// </summary>
        /// <param name="key">The baggage key</param>
        /// <param name="value">The baggage value</param>
        void AddBaggage(string key, string value);
    }
}
