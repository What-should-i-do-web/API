using WhatShouldIDo.Domain.Exceptions;

namespace WhatShouldIDo.Domain.Entities
{
    /// <summary>
    /// Represents a user's explicit taste preferences profile.
    /// This complements implicit learning from visit history.
    /// All weights are bounded to [0,1] range.
    /// </summary>
    public class UserTasteProfile : EntityBase
    {
        // Foreign Key
        public Guid UserId { get; private set; }

        // Navigation
        public User User { get; private set; } = null!;

        // ========================
        // Interest Weights (0-1)
        // ========================
        // Higher value = stronger interest in this dimension

        public double CultureWeight { get; private set; } = 0.5;
        public double FoodWeight { get; private set; } = 0.5;
        public double NatureWeight { get; private set; } = 0.5;
        public double NightlifeWeight { get; private set; } = 0.5;
        public double ShoppingWeight { get; private set; } = 0.5;
        public double ArtWeight { get; private set; } = 0.5;
        public double WellnessWeight { get; private set; } = 0.5;
        public double SportsWeight { get; private set; } = 0.5;

        // ========================
        // Preference Weights (0-1)
        // ========================
        // What aspects of places does the user value?

        public double TasteQualityWeight { get; private set; } = 0.5;
        public double AtmosphereWeight { get; private set; } = 0.5;
        public double DesignWeight { get; private set; } = 0.5;
        public double CalmnessWeight { get; private set; } = 0.5;
        public double SpaciousnessWeight { get; private set; } = 0.5;

        // ========================
        // Discovery Style (0-1)
        // ========================
        // 0 = safe/familiar, 1 = exploratory/adventurous

        public double NoveltyTolerance { get; private set; } = 0.5;

        // ========================
        // Metadata
        // ========================

        public string QuizVersion { get; private set; } = "v1";
        public DateTime CreatedAtUtc { get; private set; }
        public DateTime UpdatedAtUtc { get; private set; }
        public byte[] RowVersion { get; private set; } = null!; // Concurrency token

        // ========================
        // Constructors
        // ========================

        // EF Core constructor
        private UserTasteProfile() { }

        /// <summary>
        /// Create a new taste profile with default balanced weights (all 0.5).
        /// </summary>
        public static UserTasteProfile CreateDefault(Guid userId, string quizVersion, DateTime utcNow)
        {
            var profile = new UserTasteProfile
            {
                UserId = userId,
                QuizVersion = quizVersion,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow
            };

            profile.EnsureInvariantsOrThrow();
            return profile;
        }

        /// <summary>
        /// Create a new taste profile from quiz results.
        /// </summary>
        public static UserTasteProfile CreateFromQuiz(
            Guid userId,
            string quizVersion,
            Dictionary<string, double> weights,
            DateTime utcNow)
        {
            var profile = new UserTasteProfile
            {
                UserId = userId,
                QuizVersion = quizVersion,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow
            };

            // Apply quiz weights
            profile.ApplyWeights(weights);
            profile.EnsureInvariantsOrThrow();

            return profile;
        }

        // ========================
        // Behavior Methods
        // ========================

        /// <summary>
        /// Apply incremental delta to profile weights.
        /// Used for feedback-driven evolution.
        /// Deltas are bounded and clamped to prevent wild swings.
        /// </summary>
        public void ApplyDelta(Dictionary<string, double> deltas, DateTime utcNow)
        {
            if (deltas == null || !deltas.Any())
                return;

            const double MaxDeltaPerUpdate = 0.05; // Max ±5% change per feedback event

            foreach (var (key, rawDelta) in deltas)
            {
                // Clamp delta to prevent extreme changes
                var delta = Math.Max(-MaxDeltaPerUpdate, Math.Min(MaxDeltaPerUpdate, rawDelta));

                // Apply delta to corresponding weight
                switch (key)
                {
                    // Interests
                    case nameof(CultureWeight):
                        CultureWeight = Clamp(CultureWeight + delta);
                        break;
                    case nameof(FoodWeight):
                        FoodWeight = Clamp(FoodWeight + delta);
                        break;
                    case nameof(NatureWeight):
                        NatureWeight = Clamp(NatureWeight + delta);
                        break;
                    case nameof(NightlifeWeight):
                        NightlifeWeight = Clamp(NightlifeWeight + delta);
                        break;
                    case nameof(ShoppingWeight):
                        ShoppingWeight = Clamp(ShoppingWeight + delta);
                        break;
                    case nameof(ArtWeight):
                        ArtWeight = Clamp(ArtWeight + delta);
                        break;
                    case nameof(WellnessWeight):
                        WellnessWeight = Clamp(WellnessWeight + delta);
                        break;
                    case nameof(SportsWeight):
                        SportsWeight = Clamp(SportsWeight + delta);
                        break;

                    // Preferences
                    case nameof(TasteQualityWeight):
                        TasteQualityWeight = Clamp(TasteQualityWeight + delta);
                        break;
                    case nameof(AtmosphereWeight):
                        AtmosphereWeight = Clamp(AtmosphereWeight + delta);
                        break;
                    case nameof(DesignWeight):
                        DesignWeight = Clamp(DesignWeight + delta);
                        break;
                    case nameof(CalmnessWeight):
                        CalmnessWeight = Clamp(CalmnessWeight + delta);
                        break;
                    case nameof(SpaciousnessWeight):
                        SpaciousnessWeight = Clamp(SpaciousnessWeight + delta);
                        break;

                    // Discovery
                    case nameof(NoveltyTolerance):
                        NoveltyTolerance = Clamp(NoveltyTolerance + delta);
                        break;
                }
            }

            UpdatedAtUtc = utcNow;
            EnsureInvariantsOrThrow();
        }

        /// <summary>
        /// Apply weights from quiz or manual update.
        /// Values are clamped to [0,1] range.
        /// </summary>
        public void ApplyWeights(Dictionary<string, double> weights)
        {
            if (weights == null || !weights.Any())
                return;

            foreach (var (key, value) in weights)
            {
                var clampedValue = Clamp(value);

                switch (key)
                {
                    // Interests
                    case nameof(CultureWeight):
                        CultureWeight = clampedValue;
                        break;
                    case nameof(FoodWeight):
                        FoodWeight = clampedValue;
                        break;
                    case nameof(NatureWeight):
                        NatureWeight = clampedValue;
                        break;
                    case nameof(NightlifeWeight):
                        NightlifeWeight = clampedValue;
                        break;
                    case nameof(ShoppingWeight):
                        ShoppingWeight = clampedValue;
                        break;
                    case nameof(ArtWeight):
                        ArtWeight = clampedValue;
                        break;
                    case nameof(WellnessWeight):
                        WellnessWeight = clampedValue;
                        break;
                    case nameof(SportsWeight):
                        SportsWeight = clampedValue;
                        break;

                    // Preferences
                    case nameof(TasteQualityWeight):
                        TasteQualityWeight = clampedValue;
                        break;
                    case nameof(AtmosphereWeight):
                        AtmosphereWeight = clampedValue;
                        break;
                    case nameof(DesignWeight):
                        DesignWeight = clampedValue;
                        break;
                    case nameof(CalmnessWeight):
                        CalmnessWeight = clampedValue;
                        break;
                    case nameof(SpaciousnessWeight):
                        SpaciousnessWeight = clampedValue;
                        break;

                    // Discovery
                    case nameof(NoveltyTolerance):
                        NoveltyTolerance = clampedValue;
                        break;
                }
            }
        }

        /// <summary>
        /// Get all interest weights as a dictionary.
        /// </summary>
        public Dictionary<string, double> GetInterestWeights()
        {
            return new Dictionary<string, double>
            {
                { "Culture", CultureWeight },
                { "Food", FoodWeight },
                { "Nature", NatureWeight },
                { "Nightlife", NightlifeWeight },
                { "Shopping", ShoppingWeight },
                { "Art", ArtWeight },
                { "Wellness", WellnessWeight },
                { "Sports", SportsWeight }
            };
        }

        /// <summary>
        /// Get all preference weights as a dictionary.
        /// </summary>
        public Dictionary<string, double> GetPreferenceWeights()
        {
            return new Dictionary<string, double>
            {
                { "TasteQuality", TasteQualityWeight },
                { "Atmosphere", AtmosphereWeight },
                { "Design", DesignWeight },
                { "Calmness", CalmnessWeight },
                { "Spaciousness", SpaciousnessWeight }
            };
        }

        /// <summary>
        /// Get all weights (interests, preferences, novelty) as a dictionary.
        /// </summary>
        public Dictionary<string, double> GetAllWeights()
        {
            return new Dictionary<string, double>
            {
                // Interests
                { nameof(CultureWeight), CultureWeight },
                { nameof(FoodWeight), FoodWeight },
                { nameof(NatureWeight), NatureWeight },
                { nameof(NightlifeWeight), NightlifeWeight },
                { nameof(ShoppingWeight), ShoppingWeight },
                { nameof(ArtWeight), ArtWeight },
                { nameof(WellnessWeight), WellnessWeight },
                { nameof(SportsWeight), SportsWeight },
                // Preferences
                { nameof(TasteQualityWeight), TasteQualityWeight },
                { nameof(AtmosphereWeight), AtmosphereWeight },
                { nameof(DesignWeight), DesignWeight },
                { nameof(CalmnessWeight), CalmnessWeight },
                { nameof(SpaciousnessWeight), SpaciousnessWeight },
                // Discovery
                { nameof(NoveltyTolerance), NoveltyTolerance }
            };
        }

        /// <summary>
        /// Update profile weights from manual edit.
        /// Does NOT clamp deltas, applies weights directly (clamped to [0,1]).
        /// </summary>
        public void UpdateWeights(Dictionary<string, double> weights, DateTime utcNow)
        {
            ApplyWeights(weights);
            UpdatedAtUtc = utcNow;
            EnsureInvariantsOrThrow();
        }

        /// <summary>
        /// Update profile from quiz submission or claim.
        /// Replaces quiz version and all weights.
        /// </summary>
        public void UpdateFromQuiz(string quizVersion, Dictionary<string, double> weights, DateTime utcNow)
        {
            QuizVersion = quizVersion;
            ApplyWeights(weights);
            UpdatedAtUtc = utcNow;
            EnsureInvariantsOrThrow();
        }

        /// <summary>
        /// Validate all invariants and throw if violated.
        /// Call after any mutation.
        /// </summary>
        public void EnsureInvariantsOrThrow()
        {
            var errors = ValidateInvariants();
            if (errors.Any())
            {
                throw new DomainException($"UserTasteProfile invariants violated: {string.Join(", ", errors)}");
            }
        }

        /// <summary>
        /// Validate all invariants and return error messages.
        /// </summary>
        public List<string> ValidateInvariants()
        {
            var errors = new List<string>();

            // UserId must not be empty
            if (UserId == Guid.Empty)
                errors.Add("UserId is required");

            // QuizVersion must not be empty
            if (string.IsNullOrWhiteSpace(QuizVersion))
                errors.Add("QuizVersion is required");

            // All weights must be in [0,1] range
            ValidateWeight(nameof(CultureWeight), CultureWeight, errors);
            ValidateWeight(nameof(FoodWeight), FoodWeight, errors);
            ValidateWeight(nameof(NatureWeight), NatureWeight, errors);
            ValidateWeight(nameof(NightlifeWeight), NightlifeWeight, errors);
            ValidateWeight(nameof(ShoppingWeight), ShoppingWeight, errors);
            ValidateWeight(nameof(ArtWeight), ArtWeight, errors);
            ValidateWeight(nameof(WellnessWeight), WellnessWeight, errors);
            ValidateWeight(nameof(SportsWeight), SportsWeight, errors);
            ValidateWeight(nameof(TasteQualityWeight), TasteQualityWeight, errors);
            ValidateWeight(nameof(AtmosphereWeight), AtmosphereWeight, errors);
            ValidateWeight(nameof(DesignWeight), DesignWeight, errors);
            ValidateWeight(nameof(CalmnessWeight), CalmnessWeight, errors);
            ValidateWeight(nameof(SpaciousnessWeight), SpaciousnessWeight, errors);
            ValidateWeight(nameof(NoveltyTolerance), NoveltyTolerance, errors);

            // Timestamps must be valid
            if (CreatedAtUtc == default)
                errors.Add("CreatedAtUtc is required");

            if (UpdatedAtUtc == default)
                errors.Add("UpdatedAtUtc is required");

            if (UpdatedAtUtc < CreatedAtUtc)
                errors.Add("UpdatedAtUtc cannot be before CreatedAtUtc");

            return errors;
        }

        // ========================
        // Private Helpers
        // ========================

        private static void ValidateWeight(string name, double value, List<string> errors)
        {
            if (value < 0.0 || value > 1.0)
                errors.Add($"{name} must be in [0,1] range, got {value}");
        }

        private static double Clamp(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }
    }
}
