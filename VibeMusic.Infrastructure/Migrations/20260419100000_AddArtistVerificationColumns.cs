using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VibeMusic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArtistVerificationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "deezer_artist_id",
                table: "artists",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "verification_status",
                table: "artists",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "verified_at",
                table: "artists",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deezer_artist_id",
                table: "artists");

            migrationBuilder.DropColumn(
                name: "verification_status",
                table: "artists");

            migrationBuilder.DropColumn(
                name: "verified_at",
                table: "artists");
        }
    }
}
