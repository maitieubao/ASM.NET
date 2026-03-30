using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YoutubeMusicPlayer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArtistFollowersAndStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "dateofbirth",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "position",
                table: "playlistsongs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "visibility",
                table: "playlists",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ArtistFollowers",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ArtistId = table.Column<int>(type: "integer", nullable: false),
                    FollowedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtistFollowers", x => new { x.UserId, x.ArtistId });
                    table.ForeignKey(
                        name: "FK_ArtistFollowers_artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "artists",
                        principalColumn: "artistid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArtistFollowers_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "userid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArtistFollowers_ArtistId",
                table: "ArtistFollowers",
                column: "ArtistId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtistFollowers");

            migrationBuilder.DropColumn(
                name: "dateofbirth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "position",
                table: "playlistsongs");

            migrationBuilder.DropColumn(
                name: "visibility",
                table: "playlists");
        }
    }
}
