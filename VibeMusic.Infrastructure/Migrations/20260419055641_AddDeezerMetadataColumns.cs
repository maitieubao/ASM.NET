using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VibeMusic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeezerMetadataColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "audio_gain",
                table: "songs",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "available_countries",
                table: "songs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "bpm",
                table: "songs",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deezer_album_id",
                table: "songs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deezer_artist_id",
                table: "songs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deezer_track_id",
                table: "songs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "disk_number",
                table: "songs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "enriched_at",
                table: "songs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "popularity_rank",
                table: "songs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preview_url",
                table: "songs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "track_number",
                table: "songs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "audio_gain",
                table: "songs");

            migrationBuilder.DropColumn(
                name: "available_countries",
                table: "songs");

            migrationBuilder.DropColumn(
                name: "bpm",
                table: "songs");

            migrationBuilder.DropColumn(
                name: "deezer_album_id",
                table: "songs");

            migrationBuilder.DropColumn(
                name: "deezer_artist_id",
                table: "songs");

            migrationBuilder.DropColumn(
                name: "deezer_track_id",
                table: "songs");

            migrationBuilder.DropColumn(
                name: "disk_number",
                table: "songs");

            migrationBuilder.DropColumn(
                name: "enriched_at",
                table: "songs");

            migrationBuilder.DropColumn(
                name: "popularity_rank",
                table: "songs");

            migrationBuilder.DropColumn(
                name: "preview_url",
                table: "songs");

            migrationBuilder.DropColumn(
                name: "track_number",
                table: "songs");
        }
    }
}
