using Microsoft.EntityFrameworkCore;
using NajaEcho.Application.Abstractions;
using NajaEcho.Domain.Organizations;
using NajaEcho.Infrastructure.Persistence;

namespace NajaEcho.Infrastructure.Organizations;

/// <summary>
/// Reads and writes organization membership.
/// </summary>
/// <remarks>
/// <b>LINQ only — no raw SQL.</b> This repository is the one that manages the boundary, so a
/// <c>Database.SqlQuery</c> call here would be invisible to the very query filter this feature
/// exists to install. Enforced by a check in the polish phase (tasks.md T058).
/// </remarks>
public sealed class OrganizationRepository(AppDbContext db) : IOrganizationRepository
{
    public async Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken ct) =>
        await db.Organizations.AsNoTracking().OrderBy(o => o.Name).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid organizationId, CancellationToken ct) =>
        db.Organizations.AnyAsync(o => o.Id == organizationId, ct);

    public async Task<Organization?> GetCurrentForUserAsync(Guid userId, CancellationToken ct) =>
        await db.OrganizationMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.IsCurrent)
            .Join(db.Organizations, m => m.OrganizationId, o => o.Id, (_, o) => o)
            .SingleOrDefaultAsync(ct);

    /// <summary>
    /// Makes <paramref name="organizationId"/> current for the member, or clears it when null.
    /// Returns the organization that was current beforehand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ordering is load-bearing.</b> The existing current membership is cleared and flushed
    /// <i>before</i> the new one is set. Doing both in a single <c>SaveChanges</c> lets EF order
    /// the statements as it likes, and if the insert lands first there are momentarily two current
    /// rows — which <c>ux_organization_memberships_user_current</c> rejects, failing a change that
    /// should have succeeded. The transaction makes the pair atomic so no reader ever observes the
    /// member as belonging to nothing (FR-004).
    /// </para>
    /// <para>
    /// Joins an ambient transaction when the caller already started one, so this composes into a
    /// larger unit of work — a scoping feature (#32–#34) assigning a member and stamping their
    /// rows in one atomic step needs that, and an unconditional <c>BeginTransaction</c> would
    /// throw instead.
    /// </para>
    /// </remarks>
    public async Task<Guid?> SetCurrentAsync(Guid userId, Guid? organizationId, CancellationToken ct)
    {
        var memberships = await db.OrganizationMemberships
            .Where(m => m.UserId == userId)
            .ToListAsync(ct);

        var current = SingleCurrentOrThrow(memberships, userId);
        var previousOrganizationId = current?.OrganizationId;

        if (current?.OrganizationId == organizationId)
        {
            return previousOrganizationId; // Already current — idempotent, and nothing to write.
        }

        // Only own the transaction if nobody above us does; committing someone else's would end
        // their unit of work early.
        var ownsTransaction = db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;

        try
        {
            if (current is not null)
            {
                current.IsCurrent = false;
                await db.SaveChangesAsync(ct);
            }

            if (organizationId is not null)
            {
                var existing = memberships.SingleOrDefault(m => m.OrganizationId == organizationId);

                if (existing is not null)
                {
                    // Reactivate rather than insert: ux_organization_memberships_user_org allows
                    // only one row per (member, organization).
                    existing.IsCurrent = true;
                }
                else
                {
                    db.OrganizationMemberships.Add(new OrganizationMembership
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        OrganizationId = organizationId.Value,
                        IsCurrent = true,
                        JoinedAt = DateTimeOffset.UtcNow,
                    });
                }

                await db.SaveChangesAsync(ct);
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }

            return previousOrganizationId;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// The member's current membership, or null. Throws a named error rather than an opaque one if
    /// the at-most-one-current invariant has been violated.
    /// </summary>
    /// <remarks>
    /// A bare <c>SingleOrDefault</c> here fails with "Sequence contains more than one element",
    /// which names neither the member nor the invariant. That only happens if
    /// <c>ux_organization_memberships_user_current</c> is missing — dropped in maintenance, or
    /// absent because a database was provisioned outside the migration chain — and that is exactly
    /// the moment the operator needs to be told which member and which rule.
    /// </remarks>
    private static OrganizationMembership? SingleCurrentOrThrow(
        List<OrganizationMembership> memberships, Guid userId)
    {
        var currentMemberships = memberships.Where(m => m.IsCurrent).ToList();

        return currentMemberships.Count switch
        {
            0 => null,
            1 => currentMemberships[0],
            _ => throw new MultipleCurrentMembershipsException(
                userId, currentMemberships.Select(m => m.OrganizationId).ToList()),
        };
    }
}
