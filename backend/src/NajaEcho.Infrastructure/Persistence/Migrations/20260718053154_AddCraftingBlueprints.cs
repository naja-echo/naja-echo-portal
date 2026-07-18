using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NajaEcho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCraftingBlueprints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blueprints",
                schema: "sc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    product_entity_class = table.Column<Guid>(type: "uuid", nullable: false),
                    gear = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    subtype = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    product_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    manufacturer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: true),
                    suggested_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    suggested_product_entity_class = table.Column<Guid>(type: "uuid", nullable: true),
                    cig_data_error = table.Column<bool>(type: "boolean", nullable: true),
                    tiers = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blueprints", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "crafting_datasets",
                schema: "sc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    total_blueprints = table.Column<int>(type: "integer", nullable: false),
                    total_products = table.Column<int>(type: "integer", nullable: false),
                    total_resources = table.Column<int>(type: "integer", nullable: false),
                    total_items = table.Column<int>(type: "integer", nullable: false),
                    efficiency = table.Column<double>(type: "double precision", nullable: false),
                    dismantle_time_seconds = table.Column<int>(type: "integer", nullable: false),
                    blacklisted_resources = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    blacklisted_entity_classes = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_crafting_datasets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "crafting_materials",
                schema: "sc",
                columns: table => new
                {
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    matched_uex_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_crafting_materials", x => new { x.kind, x.name });
                });

            migrationBuilder.CreateTable(
                name: "crafting_properties",
                schema: "sc",
                columns: table => new
                {
                    property_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    unit = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    category = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    name_overrides = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_crafting_properties", x => x.property_key);
                });

            migrationBuilder.CreateTable(
                name: "blueprint_tiers",
                schema: "sc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    blueprint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier_index = table.Column<int>(type: "integer", nullable: false),
                    craft_time_seconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blueprint_tiers", x => x.id);
                    table.ForeignKey(
                        name: "fk_blueprint_tiers_blueprint_id",
                        column: x => x.blueprint_id,
                        principalSchema: "sc",
                        principalTable: "blueprints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "blueprint_slot_options",
                schema: "sc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_index = table.Column<int>(type: "integer", nullable: false),
                    slot_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    option_index = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    material_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    min_quality = table.Column<int>(type: "integer", nullable: false),
                    matched_uex_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blueprint_slot_options", x => x.id);
                    table.ForeignKey(
                        name: "fk_blueprint_slot_options_tier_id",
                        column: x => x.tier_id,
                        principalSchema: "sc",
                        principalTable: "blueprint_tiers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_blueprint_slot_options_matched_uex_id",
                schema: "sc",
                table: "blueprint_slot_options",
                column: "matched_uex_id");

            migrationBuilder.CreateIndex(
                name: "ix_blueprint_slot_options_material_name",
                schema: "sc",
                table: "blueprint_slot_options",
                column: "material_name");

            migrationBuilder.CreateIndex(
                name: "ix_blueprint_slot_options_tier_id",
                schema: "sc",
                table: "blueprint_slot_options",
                column: "tier_id");

            migrationBuilder.CreateIndex(
                name: "ux_blueprint_slot_options_tier_slot_option",
                schema: "sc",
                table: "blueprint_slot_options",
                columns: new[] { "tier_id", "slot_index", "option_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_blueprint_tiers_blueprint_id_tier_index",
                schema: "sc",
                table: "blueprint_tiers",
                columns: new[] { "blueprint_id", "tier_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_crafting_materials_matched_uex_id",
                schema: "sc",
                table: "crafting_materials",
                column: "matched_uex_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blueprint_slot_options",
                schema: "sc");

            migrationBuilder.DropTable(
                name: "crafting_datasets",
                schema: "sc");

            migrationBuilder.DropTable(
                name: "crafting_materials",
                schema: "sc");

            migrationBuilder.DropTable(
                name: "crafting_properties",
                schema: "sc");

            migrationBuilder.DropTable(
                name: "blueprint_tiers",
                schema: "sc");

            migrationBuilder.DropTable(
                name: "blueprints",
                schema: "sc");
        }
    }
}
