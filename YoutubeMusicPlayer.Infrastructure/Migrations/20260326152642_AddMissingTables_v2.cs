using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace YoutubeMusicPlayer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingTables_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_playlists_users_userid",
                table: "playlists");

            migrationBuilder.AddColumn<bool>(
                name: "islocked",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "total_listen_seconds",
                table: "users",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "categoryid",
                table: "songs",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "userid",
                table: "playlists",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "featuredtype",
                table: "playlists",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "isfeatured",
                table: "playlists",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "genres",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    categoryid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.categoryid);
                });

            migrationBuilder.CreateTable(
                name: "comments",
                columns: table => new
                {
                    commentid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: false),
                    songid = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comments", x => x.commentid);
                    table.ForeignKey(
                        name: "FK_comments_songs_songid",
                        column: x => x.songid,
                        principalTable: "songs",
                        principalColumn: "songid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_comments_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "userid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    notificationid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: true),
                    title = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: true),
                    isread = table.Column<bool>(type: "boolean", nullable: false),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.notificationid);
                    table.ForeignKey(
                        name: "FK_notifications_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "userid");
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    reportid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: false),
                    targettype = table.Column<string>(type: "text", nullable: false),
                    targetid = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    details = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolvedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.reportid);
                    table.ForeignKey(
                        name: "FK_reports_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "userid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "songlikes",
                columns: table => new
                {
                    userid = table.Column<int>(type: "integer", nullable: false),
                    songid = table.Column<int>(type: "integer", nullable: false),
                    likedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_songlikes", x => new { x.userid, x.songid });
                    table.ForeignKey(
                        name: "FK_songlikes_songs_songid",
                        column: x => x.songid,
                        principalTable: "songs",
                        principalColumn: "songid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_songlikes_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "userid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_genre_stats",
                columns: table => new
                {
                    statid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: false),
                    genre_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    listen_seconds = table.Column<double>(type: "double precision", nullable: false),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_genre_stats", x => x.statid);
                });

            migrationBuilder.CreateIndex(
                name: "IX_songs_categoryid",
                table: "songs",
                column: "categoryid");

            migrationBuilder.CreateIndex(
                name: "IX_comments_songid",
                table: "comments",
                column: "songid");

            migrationBuilder.CreateIndex(
                name: "IX_comments_userid",
                table: "comments",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_userid",
                table: "notifications",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_reports_userid",
                table: "reports",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_songlikes_songid",
                table: "songlikes",
                column: "songid");

            migrationBuilder.AddForeignKey(
                name: "FK_playlists_users_userid",
                table: "playlists",
                column: "userid",
                principalTable: "users",
                principalColumn: "userid");

            migrationBuilder.AddForeignKey(
                name: "FK_songs_categories_categoryid",
                table: "songs",
                column: "categoryid",
                principalTable: "categories",
                principalColumn: "categoryid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_playlists_users_userid",
                table: "playlists");

            migrationBuilder.DropForeignKey(
                name: "FK_songs_categories_categoryid",
                table: "songs");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "comments");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "songlikes");

            migrationBuilder.DropTable(
                name: "user_genre_stats");

            migrationBuilder.DropIndex(
                name: "IX_songs_categoryid",
                table: "songs");

            migrationBuilder.DropColumn(
                name: "islocked",
                table: "users");

            migrationBuilder.DropColumn(
                name: "total_listen_seconds",
                table: "users");

            migrationBuilder.DropColumn(
                name: "categoryid",
                table: "songs");

            migrationBuilder.DropColumn(
                name: "featuredtype",
                table: "playlists");

            migrationBuilder.DropColumn(
                name: "isfeatured",
                table: "playlists");

            migrationBuilder.DropColumn(
                name: "description",
                table: "genres");

            migrationBuilder.AlterColumn<int>(
                name: "userid",
                table: "playlists",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_playlists_users_userid",
                table: "playlists",
                column: "userid",
                principalTable: "users",
                principalColumn: "userid",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
