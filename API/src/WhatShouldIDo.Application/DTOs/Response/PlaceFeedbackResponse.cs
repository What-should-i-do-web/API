using System;
using System.Collections.Generic;

namespace WhatShouldIDo.Application.DTOs.Response
{
    /// <summary>
    /// Response after applying place feedback.
    /// </summary>
    public class PlaceFeedbackResponse
    {
        /// <summary>
        /// Success indicator.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Updated profile summary showing how feedback affected weights.
        /// </summary>
        public TasteProfileSummaryDto? UpdatedProfile { get; set; }

        /// <summary>
        /// Deltas that were applied (for transparency/debugging).
        /// </summary>
        public Dictionary<string, double>? AppliedDeltas { get; set; }

        /// <summary>
        /// Message describing the feedback result.
        /// </summary>
        public string? Message { get; set; }
    }
}
