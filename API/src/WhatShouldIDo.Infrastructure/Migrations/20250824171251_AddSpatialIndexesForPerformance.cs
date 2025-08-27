using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatShouldIDo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSpatialIndexesForPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add spatial indexes for Places table (primary location queries)
            migrationBuilder.CreateIndex(
                name: "IX_Places_Location_Spatial",
                table: "Places",
                columns: new[] { "Latitude", "Longitude" });

            // Note: Skipping Source index due to nvarchar(max) type limitation

            // Add index for Places cache expiration queries
            migrationBuilder.CreateIndex(
                name: "IX_Places_CachedAt",
                table: "Places",
                column: "CachedAt");

            // Add spatial indexes for Suggestions table
            migrationBuilder.CreateIndex(
                name: "IX_Suggestions_Location_Spatial", 
                table: "Suggestions",
                columns: new[] { "Latitude", "Longitude" });

            // Add index for Suggestions CreatedAt for time-based queries
            migrationBuilder.CreateIndex(
                name: "IX_Suggestions_CreatedAt",
                table: "Suggestions",
                column: "CreatedAt");

            // Add spatial indexes for Pois table
            migrationBuilder.CreateIndex(
                name: "IX_Pois_Location_Spatial",
                table: "Pois", 
                columns: new[] { "Latitude", "Longitude" });

            // Add spatial indexes for RoutePoints table
            migrationBuilder.CreateIndex(
                name: "IX_RoutePoints_Location_Spatial",
                table: "RoutePoints",
                columns: new[] { "Latitude", "Longitude" });

            // Add spatial indexes for UserVisits table for analytics
            migrationBuilder.CreateIndex(
                name: "IX_UserVisits_Location_VisitDate",
                table: "UserVisits",
                columns: new[] { "Latitude", "Longitude", "VisitDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop spatial indexes in reverse order
            migrationBuilder.DropIndex(
                name: "IX_UserVisits_Location_VisitDate",
                table: "UserVisits");

            migrationBuilder.DropIndex(
                name: "IX_RoutePoints_Location_Spatial",
                table: "RoutePoints");

            migrationBuilder.DropIndex(
                name: "IX_Pois_Location_Spatial",
                table: "Pois");

            migrationBuilder.DropIndex(
                name: "IX_Suggestions_CreatedAt",
                table: "Suggestions");

            migrationBuilder.DropIndex(
                name: "IX_Suggestions_Location_Spatial",
                table: "Suggestions");

            migrationBuilder.DropIndex(
                name: "IX_Places_CachedAt",
                table: "Places");

            // Note: Source index was skipped in Up migration

            migrationBuilder.DropIndex(
                name: "IX_Places_Location_Spatial",
                table: "Places");
        }
    }
}
