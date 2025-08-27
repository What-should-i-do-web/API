using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatShouldIDo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserManagementSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubscriptionTier = table.Column<int>(type: "int", nullable: false),
                    SubscriptionExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PreferredCuisines = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActivityPreferences = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BudgetRange = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MobilityLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DailyApiUsage = table.Column<int>(type: "int", nullable: false),
                    LastApiReset = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Age = table.Column<int>(type: "int", nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TravelStyle = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsLocal = table.Column<bool>(type: "bit", nullable: false),
                    FavoriteCuisines = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FavoriteActivityTypes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvoidedActivityTypes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimePreferences = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TypicalBudgetPerDay = table.Column<int>(type: "int", nullable: true),
                    PreferredRadius = table.Column<int>(type: "int", nullable: true),
                    PersonalizationScore = table.Column<float>(type: "real", nullable: false),
                    LastPreferenceUpdate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserVisits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlaceName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Latitude = table.Column<float>(type: "real", nullable: false),
                    Longitude = table.Column<float>(type: "real", nullable: false),
                    VisitDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: true),
                    CompanionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserRating = table.Column<float>(type: "real", nullable: true),
                    UserReview = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    WouldRecommend = table.Column<bool>(type: "bit", nullable: false),
                    WouldVisitAgain = table.Column<bool>(type: "bit", nullable: false),
                    WeatherCondition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TimeOfDay = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DayOfWeek = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OriginalSuggestionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VisitConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserVisits_Places_PlaceId",
                        column: x => x.PlaceId,
                        principalTable: "Places",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserVisits_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_UserId",
                table: "UserProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserVisits_PlaceId",
                table: "UserVisits",
                column: "PlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserVisits_UserId_PlaceId_VisitDate",
                table: "UserVisits",
                columns: new[] { "UserId", "PlaceId", "VisitDate" });

            migrationBuilder.CreateIndex(
                name: "IX_UserVisits_VisitDate",
                table: "UserVisits",
                column: "VisitDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "UserVisits");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
