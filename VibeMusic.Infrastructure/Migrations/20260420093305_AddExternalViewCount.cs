using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VibeMusic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalViewCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_songs_youtubevideoid",
                table: "songs");

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
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "enriched_at",
                table: "songs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "popularity_rank",
                table: "songs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "preview_url",
                table: "songs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "priority_source",
                table: "songs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "track_number",
                table: "songs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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

            migrationBuilder.CreateTable(
                name: "external_view_counts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    song_id = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<int>(type: "integer", nullable: false),
                    view_count = table.Column<long>(type: "bigint", nullable: false),
                    last_updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_view_counts", x => x.id);
                    table.ForeignKey(
                        name: "FK_external_view_counts_songs_song_id",
                        column: x => x.song_id,
                        principalTable: "songs",
                        principalColumn: "songid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_songs_youtubevideoid",
                table: "songs",
                column: "youtubevideoid",
                unique: true,
                filter: "\"is_deleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_external_view_counts_song_id_source",
                table: "external_view_counts",
                columns: new[] { "song_id", "source" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "external_view_counts");

            migrationBuilder.DropIndex(
                name: "IX_songs_youtubevideoid",
                table: "songs");

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
                name: "priority_source",
                table: "songs");

            migrationBuilder.DropColumn(
                name: "track_number",
                table: "songs");

            migrationBuilder.DropColumn(
                name: "deezer_artist_id",
                table: "artists");

            migrationBuilder.DropColumn(
                name: "verification_status",
                table: "artists");

            migrationBuilder.DropColumn(
                name: "verified_at",
                table: "artists");

            migrationBuilder.CreateIndex(
                name: "IX_songs_youtubevideoid",
                table: "songs",
                column: "youtubevideoid");
        }
    }
}
