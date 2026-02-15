using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WhatShouldIDo.Application.Models;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Hybrid scoring service that combines implicit learning, explicit taste profile,
    /// novelty, context, and quality scores into a final recommendation score.
    /// This is the main integration point for the taste profile system.
    /// </summary>
    public interface IHybridScorer
    {
        /// <summary>
        /// Score and explain a list of candidate places for a user.
        /// Returns scored places with reasons for explainability.
        /// </summary>
        /// <param name="userId">User ID for personalization.</param>
        /// <param name="candidates">Candidate places to score.</param>
        /// <param name="context">Scoring context (preferences, location, time, etc.).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Scored places with explanations, sorted by score (descending).</returns>
        Task<List<ScoredPlace>> ScoreAndExplainAsync(
            Guid userId,
            List<Place> candidates,
            ScoringContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Score a single place for a user.
        /// Useful for re-scoring or filtering individual places.
        /// </summary>
        Task<ScoredPlace> ScorePlaceAsync(
            Guid userId,
            Place place,
            ScoringContext context,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Implicit scorer based on learned preferences from visit history.
    /// This is the existing scoring logic extracted from SmartSuggestionService.
    /// </summary>
    public interface IImplicitScorer
    {
        /// <summary>
        /// Calculate implicit score based on user's visit history and learned preferences.
        /// Returns score in [0,1] range.
        /// </summary>
        Task<double> ScoreAsync(
            Guid userId,
            Place place,
            UserPreferences? preferences,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Explicit scorer based on taste profile weights from quiz and feedback.
    /// </summary>
    public interface IExplicitScorer
    {
        /// <summary>
        /// Calculate explicit score based on user's taste profile.
        /// Returns score in [0,1] range.
        /// Returns 0.5 (neutral) if user has no taste profile.
        /// </summary>
        Task<double> ScoreAsync(
            UserTasteProfile? profile,
            Place place,
            CancellationToken cancellationToken = default);
    }
}
