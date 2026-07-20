namespace NajaEcho.Infrastructure.Persistence;

/// <summary>
/// The seed and backfill statements executed by the <c>AddOrganizations</c> migration.
/// </summary>
/// <remarks>
/// <para>
/// <b>These constants are frozen.</b> They are referenced by a migration that has shipped, and a
/// migration must always mean what it meant when it was applied. Editing them retroactively
/// changes history for any database that has not yet run it while doing nothing to one that has.
/// Behaviour changes belong in a new migration with new statements, not here.
/// </para>
/// <para>
/// They live outside the migration file so the integration tests can execute the exact SQL that
/// ships rather than a hand-copied paraphrase of it. Respawn truncates row data between tests, so
/// a test cannot simply assert against what the migration left behind — it has to re-run these
/// statements against a known set of users, which only works if "these statements" is one thing
/// and not two that can drift apart.
/// </para>
/// <para>
/// The organization id literal is duplicated from <c>DefaultOrganization.Id</c> deliberately:
/// Infrastructure could reference the Domain constant, but a migration that interpolates a value
/// from code is a migration whose meaning changes when that code changes.
/// </para>
/// </remarks>
public static class OrganizationSeedSql
{
    /// <summary>
    /// Creates the default organization. Guarded so re-running the migration creates no duplicate
    /// (spec FR-009).
    /// </summary>
    public const string SeedDefaultOrganization = """
        INSERT INTO organizations (id, name, created_at)
        VALUES ('9b8ac811-3cec-421c-8cfb-cc56f775ad5a', 'Naja Echo', NOW())
        ON CONFLICT (id) DO NOTHING;
        """;

    /// <summary>
    /// Gives every existing member a current membership in the default organization (spec FR-008).
    /// </summary>
    /// <remarks>
    /// The <c>NOT EXISTS</c> guard keys on the member having <i>any</i> membership, not a current
    /// one. Re-running must not resurrect a membership an admin has since cleared — that would
    /// silently undo an administrative decision, which is worse than leaving a member unassigned.
    ///
    /// <c>"AspNetUsers"</c> must stay quoted: the Identity tables kept their PascalCase names when
    /// the snake_case convention was introduced, so an unquoted reference folds to <c>aspnetusers</c>
    /// and fails to resolve.
    /// </remarks>
    public const string BackfillMemberships = """
        INSERT INTO organization_memberships (id, user_id, organization_id, is_current, joined_at)
        SELECT gen_random_uuid(), u.id, '9b8ac811-3cec-421c-8cfb-cc56f775ad5a', true, NOW()
        FROM "AspNetUsers" u
        WHERE NOT EXISTS (
            SELECT 1 FROM organization_memberships m WHERE m.user_id = u.id
        );
        """;
}
