using System.Collections.Generic;

namespace WhatShouldIDo.Application.DTOs.Response
{
    /// <summary>
    /// Server-driven taste quiz definition response.
    /// All text is localized based on Accept-Language header.
    /// </summary>
    public class TasteQuizDto
    {
        /// <summary>
        /// Quiz version identifier (e.g., "v1").
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Quiz steps/questions.
        /// </summary>
        public List<TasteQuizStepDto> Steps { get; set; } = new();
    }

    /// <summary>
    /// A single step/question in the taste quiz.
    /// </summary>
    public class TasteQuizStepDto
    {
        /// <summary>
        /// Step identifier (e.g., "interests", "preferences").
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Question type: "multi-select", "single-select", "rating".
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Localized question title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Localized question description (optional).
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Available options/answers.
        /// </summary>
        public List<TasteQuizOptionDto> Options { get; set; } = new();
    }

    /// <summary>
    /// A single option/answer in a quiz step.
    /// </summary>
    public class TasteQuizOptionDto
    {
        /// <summary>
        /// Option identifier (e.g., "culture", "food").
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Localized option label.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Optional image URL for visual quiz.
        /// </summary>
        public string? ImageUrl { get; set; }
    }
}
