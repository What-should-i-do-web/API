using Microsoft.Extensions.Logging;

namespace WhatShouldIDo.Infrastructure.Services.AI
{
    /// <summary>
    /// Helper class for implementing diversity algorithms in recommendations.
    /// Balances exploitation (familiar preferences) vs exploration (novel experiences).
    /// </summary>
    public class DiversityHelper
    {
        private readonly ILogger<DiversityHelper> _logger;
        private readonly Random _random;

        public DiversityHelper(ILogger<DiversityHelper> logger)
        {
            _logger = logger;
            _random = new Random();
        }

        /// <summary>
        /// ε-greedy selection: With probability ε, select random item (exploration).
        /// With probability (1-ε), select best item (exploitation).
        /// </summary>
        /// <typeparam name="T">Type of items to select from</typeparam>
        /// <param name="items">List of items with scores</param>
        /// <param name="epsilon">Exploration probability (0.0 - 1.0). Typical: 0.1-0.3</param>
        /// <param name="count">Number of items to select</param>
        /// <returns>Selected items with their scores</returns>
        public List<(T item, double score)> EpsilonGreedySelection<T>(
            List<(T item, double score)> items,
            double epsilon,
            int count)
        {
            if (items == null || items.Count == 0)
                return new List<(T, double)>();

            if (epsilon < 0 || epsilon > 1)
                throw new ArgumentException("Epsilon must be between 0 and 1", nameof(epsilon));

            count = Math.Min(count, items.Count);
            var selected = new List<(T item, double score)>();
            var remainingItems = new List<(T item, double score)>(items);

            for (int i = 0; i < count; i++)
            {
                if (remainingItems.Count == 0)
                    break;

                (T item, double score) selectedItem;

                // With probability ε, explore (random selection)
                if (_random.NextDouble() < epsilon)
                {
                    var randomIndex = _random.Next(remainingItems.Count);
                    selectedItem = remainingItems[randomIndex];
                    _logger.LogDebug("ε-greedy: Exploring - selected random item at index {Index}", randomIndex);
                }
                // With probability (1-ε), exploit (best score)
                else
                {
                    selectedItem = remainingItems
                        .OrderByDescending(x => x.score)
                        .First();
                    _logger.LogDebug("ε-greedy: Exploiting - selected best item with score {Score}", selectedItem.score);
                }

                selected.Add(selectedItem);
                remainingItems.Remove(selectedItem);
            }

            return selected;
        }

        /// <summary>
        /// Softmax selection: Converts scores to probabilities using temperature parameter.
        /// Higher temperature = more exploration, lower = more exploitation.
        /// </summary>
        /// <typeparam name="T">Type of items to select from</typeparam>
        /// <param name="items">List of items with scores</param>
        /// <param name="temperature">Temperature parameter (0.1 - 10.0). Default: 1.0</param>
        /// <param name="count">Number of items to select</param>
        /// <returns>Selected items with their original scores</returns>
        public List<(T item, double score)> SoftmaxSelection<T>(
            List<(T item, double score)> items,
            double temperature,
            int count)
        {
            if (items == null || items.Count == 0)
                return new List<(T, double)>();

            if (temperature <= 0)
                throw new ArgumentException("Temperature must be positive", nameof(temperature));

            count = Math.Min(count, items.Count);
            var selected = new List<(T item, double score)>();
            var remainingItems = new List<(T item, double score)>(items);

            for (int i = 0; i < count; i++)
            {
                if (remainingItems.Count == 0)
                    break;

                // Calculate softmax probabilities
                var maxScore = remainingItems.Max(x => x.score);
                var expScores = remainingItems
                    .Select(x => Math.Exp((x.score - maxScore) / temperature))
                    .ToList();

                var sumExp = expScores.Sum();
                var probabilities = expScores.Select(e => e / sumExp).ToList();

                // Sample based on probabilities
                var randomValue = _random.NextDouble();
                var cumulativeProbability = 0.0;
                int selectedIndex = 0;

                for (int j = 0; j < probabilities.Count; j++)
                {
                    cumulativeProbability += probabilities[j];
                    if (randomValue <= cumulativeProbability)
                    {
                        selectedIndex = j;
                        break;
                    }
                }

                var selectedItem = remainingItems[selectedIndex];
                selected.Add(selectedItem);
                remainingItems.RemoveAt(selectedIndex);

                _logger.LogDebug("Softmax: Selected item with score {Score} (probability {Prob:F3})",
                    selectedItem.score, probabilities[selectedIndex]);
            }

            return selected;
        }

        /// <summary>
        /// Calculates cosine similarity between two embedding vectors.
        /// Returns value between -1 (opposite) and 1 (identical).
        /// </summary>
        /// <param name="vector1">First embedding vector</param>
        /// <param name="vector2">Second embedding vector</param>
        /// <returns>Cosine similarity score</returns>
        public static double CosineSimilarity(float[] vector1, float[] vector2)
        {
            if (vector1 == null || vector2 == null)
                throw new ArgumentNullException("Vectors cannot be null");

            if (vector1.Length != vector2.Length)
                throw new ArgumentException("Vectors must have the same dimensions");

            if (vector1.Length == 0)
                return 0.0;

            double dotProduct = 0.0;
            double magnitude1 = 0.0;
            double magnitude2 = 0.0;

            for (int i = 0; i < vector1.Length; i++)
            {
                dotProduct += vector1[i] * vector2[i];
                magnitude1 += vector1[i] * vector1[i];
                magnitude2 += vector2[i] * vector2[i];
            }

            magnitude1 = Math.Sqrt(magnitude1);
            magnitude2 = Math.Sqrt(magnitude2);

            if (magnitude1 == 0.0 || magnitude2 == 0.0)
                return 0.0;

            return dotProduct / (magnitude1 * magnitude2);
        }

        /// <summary>
        /// Thompson Sampling: Bayesian approach to exploration-exploitation.
        /// Samples from Beta distribution based on historical success/failure.
        /// </summary>
        /// <typeparam name="T">Type of items to select from</typeparam>
        /// <param name="items">List of items with success/failure counts</param>
        /// <param name="count">Number of items to select</param>
        /// <returns>Selected items with their sampled scores</returns>
        public List<(T item, double sampledScore)> ThompsonSampling<T>(
            List<(T item, int successCount, int failureCount)> items,
            int count)
        {
            if (items == null || items.Count == 0)
                return new List<(T, double)>();

            count = Math.Min(count, items.Count);
            var selected = new List<(T item, double sampledScore)>();

            // Sample from Beta distribution for each item
            var sampledItems = items.Select(item =>
            {
                // Beta(α, β) where α = successes + 1, β = failures + 1
                var alpha = item.successCount + 1;
                var beta = item.failureCount + 1;

                // Simple approximation of Beta distribution using two Gamma samples
                var sample = SampleBeta(alpha, beta);

                return (item: item.item, sampledScore: sample);
            }).OrderByDescending(x => x.sampledScore)
              .Take(count)
              .ToList();

            return sampledItems;
        }

        /// <summary>
        /// Diversifies a ranked list by re-ranking based on diversity from already selected items.
        /// Useful for ensuring variety in recommendations.
        /// </summary>
        /// <typeparam name="T">Type of items</typeparam>
        /// <param name="items">Ranked items with similarity function</param>
        /// <param name="count">Number of items to select</param>
        /// <param name="lambda">Trade-off between relevance and diversity (0-1). 0=only relevance, 1=only diversity</param>
        /// <param name="similarityFunc">Function to calculate similarity between two items</param>
        /// <returns>Diversified selection</returns>
        public List<(T item, double finalScore)> MaximalMarginalRelevance<T>(
            List<(T item, double relevanceScore)> items,
            int count,
            double lambda,
            Func<T, T, double> similarityFunc)
        {
            if (items == null || items.Count == 0)
                return new List<(T, double)>();

            if (lambda < 0 || lambda > 1)
                throw new ArgumentException("Lambda must be between 0 and 1", nameof(lambda));

            count = Math.Min(count, items.Count);
            var selected = new List<(T item, double finalScore)>();
            var remainingItems = new List<(T item, double relevanceScore)>(items);

            while (selected.Count < count && remainingItems.Count > 0)
            {
                double bestScore = double.MinValue;
                int bestIndex = 0;

                // Find item that maximizes: λ * relevance - (1-λ) * max_similarity_to_selected
                for (int i = 0; i < remainingItems.Count; i++)
                {
                    var candidate = remainingItems[i];
                    var relevance = candidate.relevanceScore;

                    // Calculate maximum similarity to already selected items
                    var maxSimilarity = 0.0;
                    if (selected.Count > 0)
                    {
                        maxSimilarity = selected.Max(s => similarityFunc(candidate.item, s.item));
                    }

                    // MMR score: balance relevance and diversity
                    var mmrScore = lambda * relevance - (1 - lambda) * maxSimilarity;

                    if (mmrScore > bestScore)
                    {
                        bestScore = mmrScore;
                        bestIndex = i;
                    }
                }

                var selectedItem = remainingItems[bestIndex];
                selected.Add((selectedItem.item, bestScore));
                remainingItems.RemoveAt(bestIndex);

                _logger.LogDebug("MMR: Selected item with relevance {Relevance:F3} and MMR score {Score:F3}",
                    selectedItem.relevanceScore, bestScore);
            }

            return selected;
        }

        // Helper method to sample from Beta distribution (approximation)
        private double SampleBeta(int alpha, int beta)
        {
            // Using a simple approximation: Beta(α,β) ≈ Gamma(α) / (Gamma(α) + Gamma(β))
            var gammaAlpha = SampleGamma(alpha);
            var gammaBeta = SampleGamma(beta);
            return gammaAlpha / (gammaAlpha + gammaBeta);
        }

        // Helper method to sample from Gamma distribution (approximation)
        private double SampleGamma(int shape)
        {
            // Simple approximation using sum of exponential random variables
            double sum = 0.0;
            for (int i = 0; i < shape; i++)
            {
                sum += -Math.Log(_random.NextDouble());
            }
            return sum;
        }
    }
}
