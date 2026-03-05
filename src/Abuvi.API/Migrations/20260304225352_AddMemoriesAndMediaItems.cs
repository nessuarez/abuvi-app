using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMemoriesAndMediaItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "memories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: true),
                    camp_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_approved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memories", x => x.id);
                    table.ForeignKey(
                        name: "FK_memories_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "media_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    thumbnail_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    year = table.Column<int>(type: "integer", nullable: true),
                    decade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    memory_id = table.Column<Guid>(type: "uuid", nullable: true),
                    camp_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_approved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    context = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_media_items_memories_memory_id",
                        column: x => x.memory_id,
                        principalTable: "memories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_media_items_users_uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_media_items_approved_published",
                table: "media_items",
                columns: new[] { "is_approved", "is_published" });

            migrationBuilder.CreateIndex(
                name: "ix_media_items_context",
                table: "media_items",
                column: "context");

            migrationBuilder.CreateIndex(
                name: "ix_media_items_memory_id",
                table: "media_items",
                column: "memory_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_items_uploaded_by_user_id",
                table: "media_items",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_items_year",
                table: "media_items",
                column: "year");

            migrationBuilder.CreateIndex(
                name: "ix_memories_approved_published",
                table: "memories",
                columns: new[] { "is_approved", "is_published" });

            migrationBuilder.CreateIndex(
                name: "ix_memories_author_user_id",
                table: "memories",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_memories_year",
                table: "memories",
                column: "year");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "media_items");

            migrationBuilder.DropTable(
                name: "memories");
        }
    }
}
