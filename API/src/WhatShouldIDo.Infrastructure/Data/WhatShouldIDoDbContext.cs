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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure entity relationships, constraints, indexes here
            base.OnModelCreating(modelBuilder);
            // RoutePoint içindeki Coordinates’ı owned olarak tanıt
            modelBuilder.Entity<RoutePoint>()
                .OwnsOne(rp => rp.Location, nav =>
                {
                    nav.Property(p => p.Latitude).HasColumnName("Latitude");
                    nav.Property(p => p.Longitude).HasColumnName("Longitude");
                });

            // Poi içindeki Coordinates’ı owned olarak tanıt
            modelBuilder.Entity<Poi>()
                .OwnsOne(p => p.Location, nav =>
                {
                    nav.Property(p => p.Latitude).HasColumnName("Latitude");
                    nav.Property(p => p.Longitude).HasColumnName("Longitude");
                });
        }
    }
}