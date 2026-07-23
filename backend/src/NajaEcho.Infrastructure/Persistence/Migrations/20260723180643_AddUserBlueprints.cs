using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NajaEcho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserBlueprints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_blueprints",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blueprint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_blueprints", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_blueprints_blueprint_id",
                        column: x => x.blueprint_id,
                        principalSchema: "sc",
                        principalTable: "blueprints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_blueprints_blueprint_id",
                schema: "public",
                table: "user_blueprints",
                column: "blueprint_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_blueprints_user_id",
                schema: "public",
                table: "user_blueprints",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_blueprints_user_blueprint",
                schema: "public",
                table: "user_blueprints",
                columns: new[] { "user_id", "blueprint_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_blueprints",
                schema: "public");
        }
    }
}
