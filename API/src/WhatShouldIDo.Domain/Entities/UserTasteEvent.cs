using System;

namespace WhatShouldIDo.Domain.Entities
{
    /// <summary>
    /// Append-only audit trail for taste profile changes.
    /// Used for analytics, debugging, and potential future ML training.
    /// No PII should be stored in this entity.
    /// </summary>
    public class UserTasteEvent : EntityBase
    {
        // Foreign Key
        public Guid UserId { get; private set; }

        // Event Type
        public string EventType { get; private set; } = null!;

        // Event Payload (JSON serialized details)
        public string Payload { get; private set; } = "{}";

        // Timestamps
        public DateTime OccurredAtUtc { get; private set; }

        // Correlation ID for distributed tracing
        public string? CorrelationId { get; private set; }

        // Event Types (constants for type safety)
        public static class EventTypes
        {
            public const string QuizCompleted = "quiz_completed";
            public const string FeedbackLike = "feedback_like";
            public const string FeedbackDislike = "feedback_dislike";
            public const string FeedbackSkip = "feedback_skip";
            public const string ManualEdit = "manual_edit";
            public const string ProfileClaimed = "profile_claimed";
        }

        // EF Core constructor
        private UserTasteEvent() { }

        /// <summary>
        /// Create a new taste event.
        /// </summary>
        public static UserTasteEvent Create(
            Guid userId,
            string eventType,
            string payload,
            DateTime occurredAtUtc,
            string? correlationId = null)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("UserId is required", nameof(userId));

            if (string.IsNullOrWhiteSpace(eventType))
                throw new ArgumentException("EventType is required", nameof(eventType));

            if (string.IsNullOrWhiteSpace(payload))
                payload = "{}";

            if (occurredAtUtc == default)
                throw new ArgumentException("OccurredAtUtc is required", nameof(occurredAtUtc));

            return new UserTasteEvent
            {
                UserId = userId,
                EventType = eventType,
                Payload = payload,
                OccurredAtUtc = occurredAtUtc,
                CorrelationId = correlationId
            };
        }

        /// <summary>
        /// Create quiz completed event.
        /// </summary>
        public static UserTasteEvent CreateQuizCompleted(
            Guid userId,
            string quizVersion,
            DateTime occurredAtUtc,
            string? correlationId = null)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                quiz_version = quizVersion,
                event_time = occurredAtUtc
            });

            return Create(userId, EventTypes.QuizCompleted, payload, occurredAtUtc, correlationId);
        }

        /// <summary>
        /// Create quiz completed event with answers.
        /// </summary>
        public static UserTasteEvent QuizCompleted(
            Guid userId,
            string quizVersion,
            Dictionary<string, string> answers,
            DateTime occurredAtUtc,
            string? correlationId = null)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                quiz_version = quizVersion,
                answers,
                event_time = occurredAtUtc
            });

            return Create(userId, EventTypes.QuizCompleted, payload, occurredAtUtc, correlationId);
        }

        /// <summary>
        /// Create feedback event (like/dislike/skip).
        /// </summary>
        public static UserTasteEvent CreateFeedbackEvent(
            Guid userId,
            string feedbackType,
            string placeId,
            string placeCategory,
            DateTime occurredAtUtc,
            string? correlationId = null)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                feedback_type = feedbackType,
                place_id = placeId,
                place_category = placeCategory,
                event_time = occurredAtUtc
            });

            var eventType = feedbackType.ToLowerInvariant() switch
            {
                "like" => EventTypes.FeedbackLike,
                "dislike" => EventTypes.FeedbackDislike,
                "skip" => EventTypes.FeedbackSkip,
                _ => throw new ArgumentException($"Invalid feedback type: {feedbackType}", nameof(feedbackType))
            };

            return Create(userId, eventType, payload, occurredAtUtc, correlationId);
        }

        /// <summary>
        /// Create profile claimed event (from anonymous quiz draft).
        /// </summary>
        public static UserTasteEvent CreateProfileClaimed(
            Guid userId,
            string claimToken,
            DateTime occurredAtUtc,
            string? correlationId = null)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                claim_token = claimToken,
                event_time = occurredAtUtc,
                source = "anonymous_draft"
            });

            return Create(userId, EventTypes.ProfileClaimed, payload, occurredAtUtc, correlationId);
        }

        /// <summary>
        /// Create manual edit event (user manually adjusted weights).
        /// </summary>
        public static UserTasteEvent CreateManualEdit(
            Guid userId,
            string changedFields,
            DateTime occurredAtUtc,
            string? correlationId = null)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                changed_fields = changedFields,
                event_time = occurredAtUtc
            });

            return Create(userId, EventTypes.ManualEdit, payload, occurredAtUtc, correlationId);
        }

        /// <summary>
        /// Create manual edit event with weight dictionary.
        /// </summary>
        public static UserTasteEvent ManualEdit(
            Guid userId,
            Dictionary<string, double> weights,
            DateTime occurredAtUtc,
            string? correlationId = null)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                weights,
                event_time = occurredAtUtc
            });

            return Create(userId, EventTypes.ManualEdit, payload, occurredAtUtc, correlationId);
        }

        /// <summary>
        /// Create feedback received event with deltas.
        /// </summary>
        public static UserTasteEvent FeedbackReceived(
            Guid userId,
            string placeCategory,
            string eventType,
            Dictionary<string, double> deltas,
            DateTime occurredAtUtc,
            string? correlationId = null)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                place_category = placeCategory,
                event_type = eventType,
                deltas,
                event_time = occurredAtUtc
            });

            return Create(userId, eventType, payload, occurredAtUtc, correlationId);
        }
    }
}
