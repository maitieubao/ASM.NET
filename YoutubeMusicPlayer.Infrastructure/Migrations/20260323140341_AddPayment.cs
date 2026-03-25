using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace YoutubeMusicPlayer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ispremium",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "listeninghistory",
                columns: table => new
                {
                    historyid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: false),
                    songid = table.Column<int>(type: "integer", nullable: false),
                    listenedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listeninghistory", x => x.historyid);
                    table.ForeignKey(
                        name: "FK_listeninghistory_songs_songid",
                        column: x => x.songid,
                        principalTable: "songs",
                        principalColumn: "songid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_listeninghistory_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "userid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "playlists",
                columns: table => new
                {
                    playlistid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    coverimageurl = table.Column<string>(type: "text", nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playlists", x => x.playlistid);
                    table.ForeignKey(
                        name: "FK_playlists_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "userid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    planid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    price = table.Column<decimal>(type: "numeric", nullable: false),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_plans", x => x.planid);
                });

            migrationBuilder.CreateTable(
                name: "usersearchhistory",
                columns: table => new
                {
                    searchid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: false),
                    searchquery = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    searchedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usersearchhistory", x => x.searchid);
                    table.ForeignKey(
                        name: "FK_usersearchhistory_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "userid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "playlistsongs",
                columns: table => new
                {
                    playlistid = table.Column<int>(type: "integer", nullable: false),
                    songid = table.Column<int>(type: "integer", nullable: false),
                    addedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playlistsongs", x => new { x.playlistid, x.songid });
                    table.ForeignKey(
                        name: "FK_playlistsongs_playlists_playlistid",
                        column: x => x.playlistid,
                        principalTable: "playlists",
                        principalColumn: "playlistid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_playlistsongs_songs_songid",
                        column: x => x.songid,
                        principalTable: "songs",
                        principalColumn: "songid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    paymentid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: false),
                    planid = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    payment_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    order_code = table.Column<long>(type: "bigint", nullable: false),
                    payos_transaction_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.paymentid);
                    table.ForeignKey(
                        name: "FK_payments_subscription_plans_planid",
                        column: x => x.planid,
                        principalTable: "subscription_plans",
                        principalColumn: "planid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payments_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "userid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_subscriptions",
                columns: table => new
                {
                    user_subscription_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: false),
                    planid = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_subscriptions", x => x.user_subscription_id);
                    table.ForeignKey(
                        name: "FK_user_subscriptions_subscription_plans_planid",
                        column: x => x.planid,
                        principalTable: "subscription_plans",
                        principalColumn: "planid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_subscriptions_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "userid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_listeninghistory_songid",
                table: "listeninghistory",
                column: "songid");

            migrationBuilder.CreateIndex(
                name: "IX_listeninghistory_userid",
                table: "listeninghistory",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_payments_planid",
                table: "payments",
                column: "planid");

            migrationBuilder.CreateIndex(
                name: "IX_payments_userid",
                table: "payments",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_playlists_userid",
                table: "playlists",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_playlistsongs_songid",
                table: "playlistsongs",
                column: "songid");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_planid",
                table: "user_subscriptions",
                column: "planid");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_userid",
                table: "user_subscriptions",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_usersearchhistory_userid",
                table: "usersearchhistory",
                column: "userid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "listeninghistory");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "playlistsongs");

            migrationBuilder.DropTable(
                name: "user_subscriptions");

            migrationBuilder.DropTable(
                name: "usersearchhistory");

            migrationBuilder.DropTable(
                name: "playlists");

            migrationBuilder.DropTable(
                name: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "ispremium",
                table: "users");
        }
    }
}
