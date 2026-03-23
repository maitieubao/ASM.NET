using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace YoutubeMusicPlayer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAndUserSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "albums",
                columns: table => new
                {
                    albumid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    albumtype = table.Column<string>(type: "text", nullable: true),
                    coverimageurl = table.Column<string>(type: "text", nullable: true),
                    releasedate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    recordlabel = table.Column<string>(type: "text", nullable: true),
                    copyrighttext = table.Column<string>(type: "text", nullable: true),
                    upc = table.Column<string>(type: "text", nullable: true),
                    isexplicit = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_albums", x => x.albumid);
                });

            migrationBuilder.CreateTable(
                name: "artists",
                columns: table => new
                {
                    artistid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    bio = table.Column<string>(type: "text", nullable: true),
                    country = table.Column<string>(type: "text", nullable: true),
                    avatarurl = table.Column<string>(type: "text", nullable: true),
                    bannerurl = table.Column<string>(type: "text", nullable: true),
                    isverified = table.Column<bool>(type: "boolean", nullable: false),
                    subscribercount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artists", x => x.artistid);
                });

            migrationBuilder.CreateTable(
                name: "genres",
                columns: table => new
                {
                    genreid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genres", x => x.genreid);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    userid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    passwordhash = table.Column<string>(type: "text", nullable: true),
                    googleid = table.Column<string>(type: "text", nullable: true),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    avatarurl = table.Column<string>(type: "text", nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.userid);
                });

            migrationBuilder.CreateTable(
                name: "songs",
                columns: table => new
                {
                    songid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    albumid = table.Column<int>(type: "integer", nullable: true),
                    duration = table.Column<int>(type: "integer", nullable: true),
                    releasedate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    youtubevideoid = table.Column<string>(type: "text", nullable: false),
                    thumbnailurl = table.Column<string>(type: "text", nullable: true),
                    lyricstext = table.Column<string>(type: "text", nullable: true),
                    lyricssyncurl = table.Column<string>(type: "text", nullable: true),
                    isrc = table.Column<string>(type: "text", nullable: true),
                    isexplicit = table.Column<bool>(type: "boolean", nullable: false),
                    playcount = table.Column<long>(type: "bigint", nullable: false),
                    ispremiumonly = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_songs", x => x.songid);
                    table.ForeignKey(
                        name: "FK_songs_albums_albumid",
                        column: x => x.albumid,
                        principalTable: "albums",
                        principalColumn: "albumid",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "albumartists",
                columns: table => new
                {
                    albumid = table.Column<int>(type: "integer", nullable: false),
                    artistid = table.Column<int>(type: "integer", nullable: false),
                    role = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_albumartists", x => new { x.albumid, x.artistid });
                    table.ForeignKey(
                        name: "FK_albumartists_albums_albumid",
                        column: x => x.albumid,
                        principalTable: "albums",
                        principalColumn: "albumid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_albumartists_artists_artistid",
                        column: x => x.artistid,
                        principalTable: "artists",
                        principalColumn: "artistid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usersessions",
                columns: table => new
                {
                    sessionid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: false),
                    token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    expiresat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    isrevoked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usersessions", x => x.sessionid);
                    table.ForeignKey(
                        name: "FK_usersessions_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "userid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "songartists",
                columns: table => new
                {
                    songid = table.Column<int>(type: "integer", nullable: false),
                    artistid = table.Column<int>(type: "integer", nullable: false),
                    role = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_songartists", x => new { x.songid, x.artistid });
                    table.ForeignKey(
                        name: "FK_songartists_artists_artistid",
                        column: x => x.artistid,
                        principalTable: "artists",
                        principalColumn: "artistid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_songartists_songs_songid",
                        column: x => x.songid,
                        principalTable: "songs",
                        principalColumn: "songid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "songgenres",
                columns: table => new
                {
                    songid = table.Column<int>(type: "integer", nullable: false),
                    genreid = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_songgenres", x => new { x.songid, x.genreid });
                    table.ForeignKey(
                        name: "FK_songgenres_genres_genreid",
                        column: x => x.genreid,
                        principalTable: "genres",
                        principalColumn: "genreid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_songgenres_songs_songid",
                        column: x => x.songid,
                        principalTable: "songs",
                        principalColumn: "songid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_albumartists_artistid",
                table: "albumartists",
                column: "artistid");

            migrationBuilder.CreateIndex(
                name: "IX_artists_name",
                table: "artists",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_songartists_artistid",
                table: "songartists",
                column: "artistid");

            migrationBuilder.CreateIndex(
                name: "IX_songgenres_genreid",
                table: "songgenres",
                column: "genreid");

            migrationBuilder.CreateIndex(
                name: "IX_songs_albumid",
                table: "songs",
                column: "albumid");

            migrationBuilder.CreateIndex(
                name: "IX_usersessions_userid",
                table: "usersessions",
                column: "userid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "albumartists");

            migrationBuilder.DropTable(
                name: "songartists");

            migrationBuilder.DropTable(
                name: "songgenres");

            migrationBuilder.DropTable(
                name: "usersessions");

            migrationBuilder.DropTable(
                name: "artists");

            migrationBuilder.DropTable(
                name: "genres");

            migrationBuilder.DropTable(
                name: "songs");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "albums");
        }
    }
}
