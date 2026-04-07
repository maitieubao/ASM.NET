using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace YoutubeMusicPlayer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                    isexplicit = table.Column<bool>(type: "boolean", nullable: false),
                    deezer_album_id = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
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
                    subscribercount = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artists", x => x.artistid);
                });

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
                name: "genres",
                columns: table => new
                {
                    genreid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genres", x => x.genreid);
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
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_plans", x => x.planid);
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
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dateofbirth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ispremium = table.Column<bool>(type: "boolean", nullable: false),
                    islocked = table.Column<bool>(type: "boolean", nullable: false),
                    total_listen_seconds = table.Column<double>(type: "double precision", nullable: false),
                    resettoken = table.Column<string>(type: "text", nullable: true),
                    resettokenexpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.userid);
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
                name: "songs",
                columns: table => new
                {
                    songid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    albumid = table.Column<int>(type: "integer", nullable: true),
                    categoryid = table.Column<int>(type: "integer", nullable: true),
                    duration = table.Column<int>(type: "integer", nullable: true),
                    releasedate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    youtubevideoid = table.Column<string>(type: "text", nullable: false),
                    thumbnailurl = table.Column<string>(type: "text", nullable: true),
                    lyricstext = table.Column<string>(type: "text", nullable: true),
                    lyricssyncurl = table.Column<string>(type: "text", nullable: true),
                    isrc = table.Column<string>(type: "text", nullable: true),
                    isexplicit = table.Column<bool>(type: "boolean", nullable: false),
                    playcount = table.Column<long>(type: "bigint", nullable: false),
                    ispremiumonly = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
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
                    table.ForeignKey(
                        name: "FK_songs_categories_categoryid",
                        column: x => x.categoryid,
                        principalTable: "categories",
                        principalColumn: "categoryid");
                });

            migrationBuilder.CreateTable(
                name: "artist_followers",
                columns: table => new
                {
                    userid = table.Column<int>(type: "integer", nullable: false),
                    artistid = table.Column<int>(type: "integer", nullable: false),
                    followedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artist_followers", x => new { x.userid, x.artistid });
                    table.ForeignKey(
                        name: "FK_artist_followers_artists_artistid",
                        column: x => x.artistid,
                        principalTable: "artists",
                        principalColumn: "artistid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_artist_followers_users_userid",
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
                name: "playlists",
                columns: table => new
                {
                    playlistid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: true),
                    isfeatured = table.Column<bool>(type: "boolean", nullable: false),
                    featuredtype = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    coverimageurl = table.Column<string>(type: "text", nullable: true),
                    visibility = table.Column<string>(type: "text", nullable: false),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playlists", x => x.playlistid);
                    table.ForeignKey(
                        name: "FK_playlists_users_userid",
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
                name: "comments",
                columns: table => new
                {
                    commentid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: false),
                    songid = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    parentcommentid = table.Column<int>(type: "integer", nullable: true)
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
                name: "playlistsongs",
                columns: table => new
                {
                    playlistid = table.Column<int>(type: "integer", nullable: false),
                    songid = table.Column<int>(type: "integer", nullable: false),
                    addedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
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
                name: "comment_likes",
                columns: table => new
                {
                    likeid = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<int>(type: "integer", nullable: false),
                    commentid = table.Column<int>(type: "integer", nullable: false),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comment_likes", x => x.likeid);
                    table.ForeignKey(
                        name: "FK_comment_likes_comments_commentid",
                        column: x => x.commentid,
                        principalTable: "comments",
                        principalColumn: "commentid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_comment_likes_users_userid",
                        column: x => x.userid,
                        principalTable: "users",
                        principalColumn: "userid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_albumartists_artistid",
                table: "albumartists",
                column: "artistid");

            migrationBuilder.CreateIndex(
                name: "IX_albums_title",
                table: "albums",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "IX_artist_followers_artistid",
                table: "artist_followers",
                column: "artistid");

            migrationBuilder.CreateIndex(
                name: "IX_artists_name",
                table: "artists",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_comment_likes_commentid",
                table: "comment_likes",
                column: "commentid");

            migrationBuilder.CreateIndex(
                name: "IX_comment_likes_userid",
                table: "comment_likes",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_comments_songid",
                table: "comments",
                column: "songid");

            migrationBuilder.CreateIndex(
                name: "IX_comments_userid",
                table: "comments",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_listeninghistory_songid",
                table: "listeninghistory",
                column: "songid");

            migrationBuilder.CreateIndex(
                name: "IX_listeninghistory_userid",
                table: "listeninghistory",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_userid",
                table: "notifications",
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
                name: "IX_reports_userid",
                table: "reports",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_songartists_artistid",
                table: "songartists",
                column: "artistid");

            migrationBuilder.CreateIndex(
                name: "IX_songgenres_genreid",
                table: "songgenres",
                column: "genreid");

            migrationBuilder.CreateIndex(
                name: "IX_songlikes_songid",
                table: "songlikes",
                column: "songid");

            migrationBuilder.CreateIndex(
                name: "IX_songs_albumid",
                table: "songs",
                column: "albumid");

            migrationBuilder.CreateIndex(
                name: "IX_songs_categoryid",
                table: "songs",
                column: "categoryid");

            migrationBuilder.CreateIndex(
                name: "IX_songs_title_albumid",
                table: "songs",
                columns: new[] { "title", "albumid" });

            migrationBuilder.CreateIndex(
                name: "IX_songs_youtubevideoid",
                table: "songs",
                column: "youtubevideoid");

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
                name: "artist_followers");

            migrationBuilder.DropTable(
                name: "comment_likes");

            migrationBuilder.DropTable(
                name: "listeninghistory");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "playlistsongs");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "songartists");

            migrationBuilder.DropTable(
                name: "songgenres");

            migrationBuilder.DropTable(
                name: "songlikes");

            migrationBuilder.DropTable(
                name: "user_genre_stats");

            migrationBuilder.DropTable(
                name: "user_subscriptions");

            migrationBuilder.DropTable(
                name: "usersearchhistory");

            migrationBuilder.DropTable(
                name: "usersessions");

            migrationBuilder.DropTable(
                name: "comments");

            migrationBuilder.DropTable(
                name: "playlists");

            migrationBuilder.DropTable(
                name: "artists");

            migrationBuilder.DropTable(
                name: "genres");

            migrationBuilder.DropTable(
                name: "subscription_plans");

            migrationBuilder.DropTable(
                name: "songs");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "albums");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}
