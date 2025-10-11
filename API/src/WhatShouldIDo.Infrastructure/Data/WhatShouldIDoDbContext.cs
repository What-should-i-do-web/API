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

            // ✅ Tüm tablo isimlerini küçük harfe çevir (Postgres case-sensitive)
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                entity.SetTableName(entity.GetTableName()!.ToLowerInvariant());
            }
        }
    }
}
