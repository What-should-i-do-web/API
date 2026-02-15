using System.Collections.Generic;

namespace WhatShouldIDo.Application.Configuration
{
    /// <summary>
    /// Configuration for the server-driven taste quiz system.
    /// Quiz is defined in appsettings.json so it can be updated without code changes.
    /// </summary>
    public class TasteQuizOptions
    {
        /// <summary>
        /// Quiz version identifier (e.g., "v1", "v2").
        /// Used to track which quiz version a user completed.
        /// </summary>
        public string Version { get; set; } = "v1";

        /// <summary>
        /// How long to keep anonymous quiz drafts in Redis (hours).
        /// After this time, unclaimed drafts are automatically deleted.
        /// </summary>
        public int DraftTtlHours { get; set; } = 24;

        /// <summary>
        /// Quiz steps/questions definition.
        /// Each step has options that affect taste profile weights.
        /// </summary>
        public List<QuizStepDefinition> Steps { get; set; } = new();
    }

    /// <summary>
    /// Represents a single step/question in the taste quiz.
    /// </summary>
    public class QuizStepDefinition
    {
        /// <summary>
        /// Unique identifier for this step (e.g., "interests", "preferences").
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Question type: "multi-select", "single-select", "rating", "place-like-dislike".
        /// </summary>
        public string Type { get; set; } = "multi-select";

        /// <summary>
        /// Localization key for the question title (e.g., "quiz.step1.title").
        /// Actual text is resolved via LocalizationService.
        /// </summary>
        public string TitleKey { get; set; } = string.Empty;

        /// <summary>
        /// Localization key for the question description (optional).
        /// </summary>
        public string? DescriptionKey { get; set; }

        /// <summary>
        /// Available options/answers for this question.
        /// </summary>
        public List<QuizOptionDefinition> Options { get; set; } = new();
    }

    /// <summary>
    /// Represents a single option/answer in a quiz step.
    /// </summary>
    public class QuizOptionDefinition
    {
        /// <summary>
        /// Unique identifier for this option (e.g., "culture", "food").
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Localization key for the option label (e.g., "quiz.interests.culture").
        /// </summary>
        public string LabelKey { get; set; } = string.Empty;

        /// <summary>
        /// Optional image URL or icon identifier for visual quiz.
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Deltas to apply to taste profile when this option is selected.
        /// Key = weight name (e.g., "CultureWeight"), Value = delta amount (e.g., 0.15).
        /// Deltas are cumulative and clamped to [0,1] after all selections.
        /// </summary>
        public Dictionary<string, double> Deltas { get; set; } = new();
    }
}
