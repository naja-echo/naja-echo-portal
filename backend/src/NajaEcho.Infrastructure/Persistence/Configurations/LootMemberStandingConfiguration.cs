using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NajaEcho.Domain.Loot;

namespace NajaEcho.Infrastructure.Persistence.Configurations;

public sealed class LootMemberStandingConfiguration : IEntityTypeConfiguration<LootMemberStanding>
{
    public void Configure(EntityTypeBuilder<LootMemberStanding> builder)
    {
        builder.HasNoKey().ToView("loot_member_standing");

        builder.Property(l => l.MemberId).HasColumnName("member_id");
        builder.Property(l => l.DisplayName).HasColumnName("display_name");
        builder.Property(l => l.OrgPointsTotal).HasColumnName("org_points_total");
        builder.Property(l => l.LootPointsTotal).HasColumnName("loot_points_total");
        builder.Property(l => l.ClaimPriority).HasColumnName("claim_priority");
    }
}
