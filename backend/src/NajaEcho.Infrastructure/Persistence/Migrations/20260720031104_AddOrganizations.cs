using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NajaEcho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organization_memberships",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organization_memberships", x => x.id);
                    table.ForeignKey(
                        name: "fk_organization_memberships_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "public",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_organization_memberships_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_organization_memberships_organization_id",
                schema: "public",
                table: "organization_memberships",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ux_organization_memberships_user_current",
                schema: "public",
                table: "organization_memberships",
                column: "user_id",
                unique: true,
                filter: "is_current");

            migrationBuilder.CreateIndex(
                name: "ux_organization_memberships_user_org",
                schema: "public",
                table: "organization_memberships",
                columns: new[] { "user_id", "organization_id" },
                unique: true);

            // Seed the default organization and place every existing member in it. Both statements
            // are guarded, so applying this migration more than once creates no duplicate and alters
            // no membership already established (FR-009). See OrganizationSeedSql — those constants
            // are frozen precisely because this migration references them.
            migrationBuilder.Sql(OrganizationSeedSql.SeedDefaultOrganization);
            migrationBuilder.Sql(OrganizationSeedSql.BackfillMemberships);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Drops only the two tables this migration created. No pre-existing data is touched — no
        /// column is dropped, no type narrowed, no constraint removed from an existing table — so
        /// this is not a destructive migration under the constitution's Development Workflow rule
        /// and needs no recorded approval. The seeded organization and backfilled memberships go
        /// with the tables that hold them, which is the whole of what Up added.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_memberships",
                schema: "public");

            migrationBuilder.DropTable(
                name: "organizations",
                schema: "public");
        }
    }
}
