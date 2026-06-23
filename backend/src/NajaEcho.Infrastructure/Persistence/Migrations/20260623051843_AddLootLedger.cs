using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NajaEcho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLootLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "loot_ledger",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loot_ledger", x => x.id);
                    table.CheckConstraint("ck_loot_ledger_kind", "kind IN ('OrgPoints', 'LootPoints')");
                    table.CheckConstraint("ck_loot_ledger_reason", "length(btrim(reason)) >= 1");
                    table.ForeignKey(
                        name: "fk_loot_ledger_actor_id",
                        column: x => x.actor_id,
                        principalSchema: "public",
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_loot_ledger_member_id",
                        column: x => x.member_id,
                        principalSchema: "public",
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_loot_ledger_actor_id",
                schema: "public",
                table: "loot_ledger",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_loot_ledger_member_kind_created",
                schema: "public",
                table: "loot_ledger",
                columns: new[] { "member_id", "kind", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.Sql("""
                CREATE VIEW loot_member_standing AS
                SELECT
                    u.id                                                                   AS member_id,
                    COALESCE(c.name, u.display_name)                                       AS display_name,
                    COALESCE(SUM(l.amount) FILTER (WHERE l.kind = 'OrgPoints'), 0)         AS org_points_total,
                    COALESCE(SUM(l.amount) FILTER (WHERE l.kind = 'LootPoints'), 0)        AS loot_points_total,
                    COALESCE(SUM(l.amount) FILTER (WHERE l.kind = 'OrgPoints'), 0)::double precision
                        / COALESCE(NULLIF(SUM(l.amount) FILTER (WHERE l.kind = 'LootPoints'), 0), 100)
                                                                                           AS claim_priority
                FROM "AspNetUsers" u
                LEFT JOIN loot_ledger l ON l.member_id = u.id
                LEFT JOIN LATERAL (
                    SELECT name FROM characters
                    WHERE owner_user_id = u.id
                    ORDER BY created_at
                    LIMIT 1
                ) c ON TRUE
                GROUP BY u.id, u.display_name, c.name;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS loot_member_standing");

            migrationBuilder.DropTable(
                name: "loot_ledger",
                schema: "public");
        }
    }
}
