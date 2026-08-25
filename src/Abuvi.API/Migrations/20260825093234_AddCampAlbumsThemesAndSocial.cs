using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCampAlbumsThemesAndSocial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "camp_edition_id",
                table: "memories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "camp_edition_id",
                table: "media_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "comment_count",
                table: "media_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "media_source_id",
                table: "media_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_path",
                table: "media_items",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "year_source",
                table: "media_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.CreateTable(
                name: "camp_edition_attendances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_camp_edition_attendances", x => x.id);
                    table.ForeignKey(
                        name: "FK_camp_edition_attendances_camp_editions_camp_edition_id",
                        column: x => x.camp_edition_id,
                        principalTable: "camp_editions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_camp_edition_attendances_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_camp_edition_attendances_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "media_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_media_comments_media_items_media_item_id",
                        column: x => x.media_item_id,
                        principalTable: "media_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_media_comments_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "media_item_year_proposals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_year = table.Column<int>(type: "integer", nullable: false),
                    proposed_camp_edition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rationale = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_item_year_proposals", x => x.id);
                    table.ForeignKey(
                        name: "FK_media_item_year_proposals_camp_editions_proposed_camp_editi~",
                        column: x => x.proposed_camp_edition_id,
                        principalTable: "camp_editions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_media_item_year_proposals_media_items_media_item_id",
                        column: x => x.media_item_id,
                        principalTable: "media_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_media_item_year_proposals_users_proposed_by_user_id",
                        column: x => x.proposed_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "media_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contributor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contributor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    contributor_contact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    registered_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_sources", x => x.id);
                    table.ForeignKey(
                        name: "FK_media_sources_users_contributor_user_id",
                        column: x => x.contributor_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_media_sources_users_registered_by_user_id",
                        column: x => x.registered_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "media_themes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_themes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "media_comment_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_comment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_comment_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_media_comment_reports_media_comments_media_comment_id",
                        column: x => x.media_comment_id,
                        principalTable: "media_comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_media_comment_reports_users_reported_by_user_id",
                        column: x => x.reported_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "media_item_themes",
                columns: table => new
                {
                    media_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_theme_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tagged_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_item_themes", x => new { x.media_item_id, x.media_theme_id });
                    table.ForeignKey(
                        name: "FK_media_item_themes_media_items_media_item_id",
                        column: x => x.media_item_id,
                        principalTable: "media_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_media_item_themes_media_themes_media_theme_id",
                        column: x => x.media_theme_id,
                        principalTable: "media_themes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_media_item_themes_users_tagged_by_user_id",
                        column: x => x.tagged_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_memories_camp_edition_id",
                table: "memories",
                column: "camp_edition_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_items_camp_edition_id",
                table: "media_items",
                column: "camp_edition_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_items_edition_approved_published",
                table: "media_items",
                columns: new[] { "camp_edition_id", "is_approved", "is_published" });

            migrationBuilder.CreateIndex(
                name: "ix_media_items_media_source_id",
                table: "media_items",
                column: "media_source_id");

            migrationBuilder.CreateIndex(
                name: "IX_camp_edition_attendances_family_member_id",
                table: "camp_edition_attendances",
                column: "family_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_camp_edition_attendances_user_id",
                table: "camp_edition_attendances",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_camp_edition_attendances_edition_user_member",
                table: "camp_edition_attendances",
                columns: new[] { "camp_edition_id", "user_id", "family_member_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_comment_reports_reported_by_user_id",
                table: "media_comment_reports",
                column: "reported_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_comment_reports_status",
                table: "media_comment_reports",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_media_comment_reports_comment_reporter",
                table: "media_comment_reports",
                columns: new[] { "media_comment_id", "reported_by_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_comments_author_user_id",
                table: "media_comments",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_comments_item_created",
                table: "media_comments",
                columns: new[] { "media_item_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_media_item_themes_tagged_by_user_id",
                table: "media_item_themes",
                column: "tagged_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_item_themes_theme_id",
                table: "media_item_themes",
                column: "media_theme_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_item_year_proposals_item_id",
                table: "media_item_year_proposals",
                column: "media_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_media_item_year_proposals_proposed_by_user_id",
                table: "media_item_year_proposals",
                column: "proposed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_media_item_year_proposals_proposed_camp_edition_id",
                table: "media_item_year_proposals",
                column: "proposed_camp_edition_id");

            migrationBuilder.CreateIndex(
                name: "ux_media_item_year_proposals_item_user",
                table: "media_item_year_proposals",
                columns: new[] { "media_item_id", "proposed_by_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_sources_contributor_name",
                table: "media_sources",
                column: "contributor_name");

            migrationBuilder.CreateIndex(
                name: "ix_media_sources_contributor_user_id",
                table: "media_sources",
                column: "contributor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_media_sources_registered_by_user_id",
                table: "media_sources",
                column: "registered_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_media_themes_slug",
                table: "media_themes",
                column: "slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_media_items_camp_editions_camp_edition_id",
                table: "media_items",
                column: "camp_edition_id",
                principalTable: "camp_editions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_media_items_media_sources_media_source_id",
                table: "media_items",
                column: "media_source_id",
                principalTable: "media_sources",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_memories_camp_editions_camp_edition_id",
                table: "memories",
                column: "camp_edition_id",
                principalTable: "camp_editions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // ── Backfill: place existing media and memories into their edition ──
            //
            // Historically there is exactly one CampEdition per year (50 editions, 1976-2025),
            // so a known year determines the edition. The correlated COUNT(*) = 1 guard means we
            // verify that rather than assume it: any year with zero or several editions is
            // left unplaced for collaborative dating instead of guessed at.
            //
            // NB: MIN(id) is not usable here — PostgreSQL has no min() aggregate for uuid.

            migrationBuilder.Sql(@"
                UPDATE media_items m
                SET camp_edition_id = e.id, year_source = 'Uploader'
                FROM camp_editions e
                WHERE m.year = e.year
                  AND m.camp_edition_id IS NULL
                  AND (SELECT COUNT(*) FROM camp_editions e2 WHERE e2.year = e.year) = 1;
            ");

            migrationBuilder.Sql(@"
                UPDATE memories m
                SET camp_edition_id = e.id
                FROM camp_editions e
                WHERE m.year = e.year
                  AND m.camp_edition_id IS NULL
                  AND (SELECT COUNT(*) FROM camp_editions e2 WHERE e2.year = e.year) = 1;
            ");

            // ── Partial indexes EF Core cannot express ──

            // The unplaced pile is always queried as "WHERE camp_edition_id IS NULL",
            // so a partial index stays small no matter how large the archive grows.
            migrationBuilder.Sql(@"
                CREATE INDEX ix_media_items_unplaced
                ON media_items (created_at DESC)
                WHERE camp_edition_id IS NULL;
            ");

            // In PostgreSQL a NULL does not collide in a unique index, so the composite
            // unique index on (edition, user, family_member_id) does NOT stop the same
            // person declaring attendance for themselves twice. This partial index does.
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX ux_camp_edition_attendances_self
                ON camp_edition_attendances (camp_edition_id, user_id)
                WHERE family_member_id IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_camp_edition_attendances_self;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_media_items_unplaced;");

            migrationBuilder.DropForeignKey(
                name: "FK_media_items_camp_editions_camp_edition_id",
                table: "media_items");

            migrationBuilder.DropForeignKey(
                name: "FK_media_items_media_sources_media_source_id",
                table: "media_items");

            migrationBuilder.DropForeignKey(
                name: "FK_memories_camp_editions_camp_edition_id",
                table: "memories");

            migrationBuilder.DropTable(
                name: "camp_edition_attendances");

            migrationBuilder.DropTable(
                name: "media_comment_reports");

            migrationBuilder.DropTable(
                name: "media_item_themes");

            migrationBuilder.DropTable(
                name: "media_item_year_proposals");

            migrationBuilder.DropTable(
                name: "media_sources");

            migrationBuilder.DropTable(
                name: "media_comments");

            migrationBuilder.DropTable(
                name: "media_themes");

            migrationBuilder.DropIndex(
                name: "ix_memories_camp_edition_id",
                table: "memories");

            migrationBuilder.DropIndex(
                name: "ix_media_items_camp_edition_id",
                table: "media_items");

            migrationBuilder.DropIndex(
                name: "ix_media_items_edition_approved_published",
                table: "media_items");

            migrationBuilder.DropIndex(
                name: "ix_media_items_media_source_id",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "camp_edition_id",
                table: "memories");

            migrationBuilder.DropColumn(
                name: "camp_edition_id",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "comment_count",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "media_source_id",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "source_path",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "year_source",
                table: "media_items");
        }
    }
}
