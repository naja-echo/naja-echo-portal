using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NajaEcho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBlueprintPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_blueprints_product_name",
                schema: "sc",
                table: "blueprints",
                column: "product_name");

            migrationBuilder.CreateIndex(
                name: "ix_blueprint_tiers_blueprint_id_tier0",
                schema: "sc",
                table: "blueprint_tiers",
                column: "blueprint_id",
                filter: "tier_index = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_blueprints_product_name",
                schema: "sc",
                table: "blueprints");

            migrationBuilder.DropIndex(
                name: "ix_blueprint_tiers_blueprint_id_tier0",
                schema: "sc",
                table: "blueprint_tiers");
        }
    }
}
