using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NajaEcho.Domain.Organizations;
using NajaEcho.Infrastructure.Identity;

namespace NajaEcho.Infrastructure.Persistence.Configurations;

public sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("organization_memberships");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(m => m.OrganizationId).HasColumnName("organization_id").IsRequired();
        builder.Property(m => m.IsCurrent).HasColumnName("is_current").HasDefaultValue(false).IsRequired();
        builder.Property(m => m.JoinedAt).HasColumnName("joined_at").IsRequired();

        // FR-004: at most one current membership per member. The partial filter is what makes two
        // current rows physically unrepresentable — including under concurrent writes, which no
        // application-level check can guarantee.
        builder.HasIndex(m => m.UserId)
            .IsUnique()
            .HasFilter("is_current")
            .HasDatabaseName("ux_organization_memberships_user_current");

        // A member holds at most one row per organization. Re-assigning to an organization the
        // member already belongs to reactivates that row rather than inserting a duplicate.
        builder.HasIndex(m => new { m.UserId, m.OrganizationId })
            .IsUnique()
            .HasDatabaseName("ux_organization_memberships_user_org");

        // Deleting an organization that still has members is out of scope; the database should
        // refuse rather than silently orphan or cascade.
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(m => m.OrganizationId)
            .HasConstraintName("fk_organization_memberships_organization_id")
            .OnDelete(DeleteBehavior.Restrict);

        // Removing a member removes their memberships.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .HasConstraintName("fk_organization_memberships_user_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
