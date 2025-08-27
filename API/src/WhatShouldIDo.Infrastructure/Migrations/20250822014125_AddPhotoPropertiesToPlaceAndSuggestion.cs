using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatShouldIDo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoPropertiesToPlaceAndSuggestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoReference",
                table: "Suggestions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "Suggestions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoReference",
                table: "Places",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "Places",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoReference",
                table: "Suggestions");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "Suggestions");

            migrationBuilder.DropColumn(
                name: "PhotoReference",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "Places");
        }
    }
}
