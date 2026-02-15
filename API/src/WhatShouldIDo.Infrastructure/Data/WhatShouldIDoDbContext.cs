using Microsoft.EntityFrameworkCore;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Infrastructure.Data
{
    public class WhatShouldIDoDbContext : DbContext
    {
        public WhatShouldIDoDbContext(DbContextOptions<WhatShouldIDoDbContext> options)
            : base(options)
        {
        }

        public DbSet<Route> Routes { get; set; }
        public DbSet<Poi> Pois { get; set; }
        public DbSet<RoutePoint> RoutePoints { get; set; }
        public DbSet<Suggestion> Suggestions { get; set; }
        public DbSet<Place> Places { get; set; }

        // User management tables
        public DbSet<User> Users { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<UserVisit> UserVisits { get; set; }
        public DbSet<UserAction> UserActions { get; set; }

        // User personalization tables (Surprise Me feature)
        public DbSet<UserFavorite> UserFavorites { get; set; }
        public DbSet<UserExclusion> UserExclusions { get; set; }
        public DbSet<UserSuggestionHistory> UserSuggestionHistories { get; set; }
        public DbSet<UserRouteHistory> UserRouteHistories { get; set; }

        // Route sharing and versioning tables
        public DbSet<RouteShareToken> RouteShareTokens { get; set; }
        public DbSet<RouteRevision> RouteRevisions { get; set; }

        // Subscription management tables
        public DbSet<UserSubscription> UserSubscriptions { get; set; }

        // Taste profile management tables
        public DbSet<UserTasteProfile> UserTasteProfiles { get; set; }
        public DbSet<UserTasteEvent> UserTasteEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ✅ PostgreSQL schema
            modelBuilder.HasDefaultSchema("public");

            base.OnModelCreating(modelBuilder);

            // RoutePoint.Location owned type
            modelBuilder.Entity<RoutePoint>()
                .OwnsOne(rp => rp.Location, nav =>
                {
                    nav.Property(p => p.Latitude).HasColumnName("Latitude");
                    nav.Property(p => p.Longitude).HasColumnName("Longitude");
                });

            // Poi.Location owned type
            modelBuilder.Entity<Poi>()
                .OwnsOne(p => p.Location, nav =>
                {
                    nav.Property(p => p.Latitude).HasColumnName("Latitude");
                    nav.Property(p => p.Longitude).HasColumnName("Longitude");
                });

            // 🧩 User entity configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.HasIndex(u => u.Email).IsUnique();
                entity.HasIndex(u => u.UserName).IsUnique();

                entity.Property(u => u.Email).HasMaxLength(255).IsRequired();
                entity.Property(u => u.UserName).HasMaxLength(50).IsRequired();
                entity.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
                entity.Property(u => u.FirstName).HasMaxLength(100);
                entity.Property(u => u.LastName).HasMaxLength(100);

                // ✅ PostgreSQL JSON/text fields
                entity.Property(u => u.PreferredCuisines).HasColumnType("text");
                entity.Property(u => u.ActivityPreferences).HasColumnType("text");
                entity.Property(u => u.BudgetRange).HasMaxLength(20);
                entity.Property(u => u.MobilityLevel).HasMaxLength(20);
            });

            // 🧩 UserProfile entity configuration
            modelBuilder.Entity<UserProfile>(entity =>
            {
                entity.HasKey(up => up.Id);
                entity.HasIndex(up => up.UserId).IsUnique();

                entity.HasOne(up => up.User)
                      .WithOne(u => u.Profile)
                      .HasForeignKey<UserProfile>(up => up.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(up => up.City).HasMaxLength(100);
                entity.Property(up => up.Country).HasMaxLength(100);
                entity.Property(up => up.Language).HasMaxLength(10);
                entity.Property(up => up.TravelStyle).HasMaxLength(50);
                entity.Property(up => up.CompanionType).HasMaxLength(50);

                // ✅ JSON/text columns (PostgreSQL-friendly)
                entity.Property(up => up.FavoriteCuisines).HasColumnType("text");
                entity.Property(up => up.FavoriteActivityTypes).HasColumnType("text");
                entity.Property(up => up.AvoidedActivityTypes).HasColumnType("text");
                entity.Property(up => up.TimePreferences).HasColumnType("text");

                // ✅ pgvector column for preference embeddings (1536 dimensions for text-embedding-3-small)
                entity.Property(up => up.PreferenceEmbedding).HasColumnType("vector(1536)");
            });

            // 🧩 UserVisit entity configuration
            modelBuilder.Entity<UserVisit>(entity =>
            {
                entity.HasKey(uv => uv.Id);
                entity.HasIndex(uv => new { uv.UserId, uv.PlaceId, uv.VisitDate });
                entity.HasIndex(uv => uv.VisitDate);

                entity.HasOne(uv => uv.User)
                      .WithMany(u => u.VisitHistory)
                      .HasForeignKey(uv => uv.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(uv => uv.PlaceName).HasMaxLength(255).IsRequired();
                entity.Property(uv => uv.CompanionType).HasMaxLength(50);
                entity.Property(uv => uv.UserReview).HasMaxLength(1000);
                entity.Property(uv => uv.WeatherCondition).HasMaxLength(50);
                entity.Property(uv => uv.TimeOfDay).HasMaxLength(20);
                entity.Property(uv => uv.DayOfWeek).HasMaxLength(20);
                entity.Property(uv => uv.Source).HasMaxLength(50);
                entity.Property(uv => uv.OriginalSuggestionReason).HasMaxLength(500);
            });

            // 🧩 UserAction entity configuration
            modelBuilder.Entity<UserAction>(entity =>
            {
                entity.HasKey(ua => ua.Id);
                entity.HasIndex(ua => new { ua.UserId, ua.ActionTimestamp });
                entity.HasIndex(ua => new { ua.UserId, ua.ActionType });
                entity.HasIndex(ua => new { ua.IsProcessed, ua.ActionTimestamp });

                entity.HasOne(ua => ua.User)
                      .WithMany()
                      .HasForeignKey(ua => ua.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(ua => ua.PlaceId).HasMaxLength(255).IsRequired();
                entity.Property(ua => ua.PlaceName).HasMaxLength(255);
                entity.Property(ua => ua.Category).HasMaxLength(100);
                entity.Property(ua => ua.ActionType).HasMaxLength(20).IsRequired();
                entity.Property(ua => ua.Metadata).HasColumnType("text");
            });

            // 🧩 UserFavorite entity configuration (Surprise Me feature)
            modelBuilder.Entity<UserFavorite>(entity =>
            {
                entity.HasKey(uf => uf.Id);
                entity.HasIndex(uf => new { uf.UserId, uf.PlaceId }).IsUnique();
                entity.HasIndex(uf => uf.AddedAt);

                entity.HasOne(uf => uf.User)
                      .WithMany()
                      .HasForeignKey(uf => uf.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(uf => uf.PlaceId).HasMaxLength(255).IsRequired();
                entity.Property(uf => uf.PlaceName).HasMaxLength(255);
                entity.Property(uf => uf.Category).HasMaxLength(100);
                entity.Property(uf => uf.Notes).HasMaxLength(500);
            });

            // 🧩 UserExclusion entity configuration (Surprise Me feature)
            modelBuilder.Entity<UserExclusion>(entity =>
            {
                entity.HasKey(ue => ue.Id);
                entity.HasIndex(ue => new { ue.UserId, ue.PlaceId }).IsUnique();
                entity.HasIndex(ue => ue.ExpiresAt);

                entity.HasOne(ue => ue.User)
                      .WithMany()
                      .HasForeignKey(ue => ue.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(ue => ue.PlaceId).HasMaxLength(255).IsRequired();
                entity.Property(ue => ue.PlaceName).HasMaxLength(255);
                entity.Property(ue => ue.Reason).HasMaxLength(500);
            });

            // 🧩 UserSuggestionHistory entity configuration (Surprise Me feature - MRU last 20)
            modelBuilder.Entity<UserSuggestionHistory>(entity =>
            {
                entity.HasKey(ush => ush.Id);
                entity.HasIndex(ush => new { ush.UserId, ush.SequenceNumber });
                entity.HasIndex(ush => ush.SessionId);
                entity.HasIndex(ush => ush.SuggestedAt);

                entity.HasOne(ush => ush.User)
                      .WithMany()
                      .HasForeignKey(ush => ush.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(ush => ush.PlaceId).HasMaxLength(255).IsRequired();
                entity.Property(ush => ush.PlaceName).HasMaxLength(255);
                entity.Property(ush => ush.Category).HasMaxLength(100);
                entity.Property(ush => ush.Source).HasMaxLength(50);
                entity.Property(ush => ush.SessionId).HasMaxLength(50);
            });

            // 🧩 UserRouteHistory entity configuration (Surprise Me feature - MRU last 3)
            modelBuilder.Entity<UserRouteHistory>(entity =>
            {
                entity.HasKey(urh => urh.Id);
                entity.HasIndex(urh => new { urh.UserId, urh.SequenceNumber });
                entity.HasIndex(urh => urh.CreatedAt);

                entity.HasOne(urh => urh.User)
                      .WithMany()
                      .HasForeignKey(urh => urh.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(urh => urh.Route)
                      .WithMany()
                      .HasForeignKey(urh => urh.RouteId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.Property(urh => urh.RouteName).HasMaxLength(255);
                entity.Property(urh => urh.RouteDataJson).HasColumnType("text");
                entity.Property(urh => urh.Source).HasMaxLength(50);
            });

            // 🧩 RouteShareToken entity configuration (Route Sharing feature)
            modelBuilder.Entity<RouteShareToken>(entity =>
            {
                entity.HasKey(rst => rst.Id);
                entity.HasIndex(rst => rst.Token).IsUnique();
                entity.HasIndex(rst => rst.RouteId);
                entity.HasIndex(rst => new { rst.RouteId, rst.IsActive });
                entity.HasIndex(rst => rst.ExpiresAt);

                entity.HasOne(rst => rst.Route)
                      .WithMany()
                      .HasForeignKey(rst => rst.RouteId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(rst => rst.Token).HasMaxLength(32).IsRequired();
            });

            // 🧩 RouteRevision entity configuration (Route Versioning feature)
            modelBuilder.Entity<RouteRevision>(entity =>
            {
                entity.HasKey(rr => rr.Id);
                entity.HasIndex(rr => new { rr.RouteId, rr.RevisionNumber }).IsUnique();
                entity.HasIndex(rr => rr.CreatedAt);

                entity.HasOne(rr => rr.Route)
                      .WithMany()
                      .HasForeignKey(rr => rr.RouteId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(rr => rr.RouteDataJson).HasColumnType("text").IsRequired();
                entity.Property(rr => rr.Source).HasMaxLength(50).IsRequired();
                entity.Property(rr => rr.ChangeDescription).HasMaxLength(500);
            });

            // 🧩 UserSubscription entity configuration (Subscription Management)
            modelBuilder.Entity<UserSubscription>(entity =>
            {
                entity.HasKey(us => us.Id);

                // One subscription per user (unique constraint)
                entity.HasIndex(us => us.UserId).IsUnique();

                // Index for querying active subscriptions
                entity.HasIndex(us => new { us.Status, us.CurrentPeriodEndsAtUtc });

                entity.HasOne(us => us.User)
                      .WithOne()
                      .HasForeignKey<UserSubscription>(us => us.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Enum conversions stored as strings for clarity
                entity.Property(us => us.Provider)
                      .HasConversion<string>()
                      .HasMaxLength(20);

                entity.Property(us => us.Plan)
                      .HasConversion<string>()
                      .HasMaxLength(20);

                entity.Property(us => us.Status)
                      .HasConversion<string>()
                      .HasMaxLength(20);

                entity.Property(us => us.ExternalSubscriptionId)
                      .HasMaxLength(255);

                // Notes field for manual grants (max 500 chars, optional)
                entity.Property(us => us.Notes)
                      .HasMaxLength(500);

                // Concurrency token
                entity.Property(us => us.RowVersion)
                      .IsRowVersion();
            });

            // 🧩 UserTasteProfile entity configuration (Explicit Taste Profile System)
            modelBuilder.Entity<UserTasteProfile>(entity =>
            {
                entity.HasKey(utp => utp.Id);

                // One taste profile per user (unique constraint)
                entity.HasIndex(utp => utp.UserId).IsUnique();

                // Index for querying by quiz version
                entity.HasIndex(utp => utp.QuizVersion);

                // Relationship
                entity.HasOne(utp => utp.User)
                      .WithOne(u => u.TasteProfile)
                      .HasForeignKey<UserTasteProfile>(utp => utp.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Interest weights (default 0.5)
                entity.Property(utp => utp.CultureWeight).HasDefaultValue(0.5);
                entity.Property(utp => utp.FoodWeight).HasDefaultValue(0.5);
                entity.Property(utp => utp.NatureWeight).HasDefaultValue(0.5);
                entity.Property(utp => utp.NightlifeWeight).HasDefaultValue(0.5);
                entity.Property(utp => utp.ShoppingWeight).HasDefaultValue(0.5);
                entity.Property(utp => utp.ArtWeight).HasDefaultValue(0.5);
                entity.Property(utp => utp.WellnessWeight).HasDefaultValue(0.5);
                entity.Property(utp => utp.SportsWeight).HasDefaultValue(0.5);

                // Preference weights (default 0.5)
                entity.Property(utp => utp.TasteQualityWeight).HasDefaultValue(0.5);
                entity.Property(utp => utp.AtmosphereWeight).HasDefaultValue(0.5);
                entity.Property(utp => utp.DesignWeight).HasDefaultValue(0.5);
                entity.Property(utp => utp.CalmnessWeight).HasDefaultValue(0.5);
                entity.Property(utp => utp.SpaciousnessWeight).HasDefaultValue(0.5);

                // Discovery style (default 0.5)
                entity.Property(utp => utp.NoveltyTolerance).HasDefaultValue(0.5);

                // Metadata
                entity.Property(utp => utp.QuizVersion).HasMaxLength(20).IsRequired();

                // Concurrency token
                entity.Property(utp => utp.RowVersion).IsRowVersion();
            });

            // 🧩 UserTasteEvent entity configuration (Audit Trail)
            modelBuilder.Entity<UserTasteEvent>(entity =>
            {
                entity.HasKey(ute => ute.Id);

                // Composite index for querying user events by time
                entity.HasIndex(ute => new { ute.UserId, ute.OccurredAtUtc });

                // Index for event type queries
                entity.HasIndex(ute => ute.EventType);

                // Index for correlation ID (distributed tracing)
                entity.HasIndex(ute => ute.CorrelationId);

                // Relationship
                entity.HasOne<User>()
                      .WithMany()
                      .HasForeignKey(ute => ute.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Properties
                entity.Property(ute => ute.EventType).HasMaxLength(50).IsRequired();
                entity.Property(ute => ute.Payload).HasColumnType("jsonb").IsRequired(); // PostgreSQL JSONB for efficiency
                entity.Property(ute => ute.CorrelationId).HasMaxLength(100);
            });

            // ✅ Tüm tablo isimlerini küçük harfe çevir (Postgres case-sensitive)
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                entity.SetTableName(entity.GetTableName()!.ToLowerInvariant());
            }
        }
    }
}
