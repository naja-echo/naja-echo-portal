using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NajaEcho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBlueprintComponentAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "component_class",
                schema: "sc",
                table: "blueprints",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "component_grade",
                schema: "sc",
                table: "blueprints",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "component_size",
                schema: "sc",
                table: "blueprints",
                type: "smallint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "component_class",
                schema: "sc",
                table: "blueprints");

            migrationBuilder.DropColumn(
                name: "component_grade",
                schema: "sc",
                table: "blueprints");

            migrationBuilder.DropColumn(
                name: "component_size",
                schema: "sc",
                table: "blueprints");
        }
    }
}
