using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YoutubeMusicPlayer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialAndSystemFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArtistFollowers_artists_ArtistId",
                table: "ArtistFollowers");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistFollowers_users_UserId",
                table: "ArtistFollowers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ArtistFollowers",
                table: "ArtistFollowers");

            migrationBuilder.RenameTable(
                name: "ArtistFollowers",
                newName: "artist_followers");

            migrationBuilder.RenameColumn(
                name: "FollowedAt",
                table: "artist_followers",
                newName: "followedat");

            migrationBuilder.RenameColumn(
                name: "ArtistId",
                table: "artist_followers",
                newName: "artistid");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "artist_followers",
                newName: "userid");

            migrationBuilder.RenameIndex(
                name: "IX_ArtistFollowers_ArtistId",
                table: "artist_followers",
                newName: "IX_artist_followers_artistid");

            migrationBuilder.AddColumn<string>(
                name: "resettoken",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "resettokenexpiry",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "subscription_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "parentcommentid",
                table: "comments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updatedat",
                table: "comments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_artist_followers",
                table: "artist_followers",
                columns: new[] { "userid", "artistid" });

            migrationBuilder.CreateIndex(
                name: "IX_songs_title_albumid",
                table: "songs",
                columns: new[] { "title", "albumid" });

            migrationBuilder.CreateIndex(
                name: "IX_songs_youtubevideoid",
                table: "songs",
                column: "youtubevideoid");

            migrationBuilder.CreateIndex(
                name: "IX_albums_title",
                table: "albums",
                column: "title");

            migrationBuilder.AddForeignKey(
                name: "FK_artist_followers_artists_artistid",
                table: "artist_followers",
                column: "artistid",
                principalTable: "artists",
                principalColumn: "artistid",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_artist_followers_users_userid",
                table: "artist_followers",
                column: "userid",
                principalTable: "users",
                principalColumn: "userid",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_artist_followers_artists_artistid",
                table: "artist_followers");

            migrationBuilder.DropForeignKey(
                name: "FK_artist_followers_users_userid",
                table: "artist_followers");

            migrationBuilder.DropIndex(
                name: "IX_songs_title_albumid",
                table: "songs");

            migrationBuilder.DropIndex(
                name: "IX_songs_youtubevideoid",
                table: "songs");

            migrationBuilder.DropIndex(
                name: "IX_albums_title",
                table: "albums");

            migrationBuilder.DropPrimaryKey(
                name: "PK_artist_followers",
                table: "artist_followers");

            migrationBuilder.DropColumn(
                name: "resettoken",
                table: "users");

            migrationBuilder.DropColumn(
                name: "resettokenexpiry",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "parentcommentid",
                table: "comments");

            migrationBuilder.DropColumn(
                name: "updatedat",
                table: "comments");

            migrationBuilder.RenameTable(
                name: "artist_followers",
                newName: "ArtistFollowers");

            migrationBuilder.RenameColumn(
                name: "followedat",
                table: "ArtistFollowers",
                newName: "FollowedAt");

            migrationBuilder.RenameColumn(
                name: "artistid",
                table: "ArtistFollowers",
                newName: "ArtistId");

            migrationBuilder.RenameColumn(
                name: "userid",
                table: "ArtistFollowers",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_artist_followers_artistid",
                table: "ArtistFollowers",
                newName: "IX_ArtistFollowers_ArtistId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ArtistFollowers",
                table: "ArtistFollowers",
                columns: new[] { "UserId", "ArtistId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistFollowers_artists_ArtistId",
                table: "ArtistFollowers",
                column: "ArtistId",
                principalTable: "artists",
                principalColumn: "artistid",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistFollowers_users_UserId",
                table: "ArtistFollowers",
                column: "UserId",
                principalTable: "users",
                principalColumn: "userid",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
