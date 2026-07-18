using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NajaEcho.Domain.Blueprints;

namespace NajaEcho.Infrastructure.Persistence.Configurations;

public sealed class CraftingBlueprintTierConfiguration : IEntityTypeConfiguration<CraftingBlueprintTier>
{
    public void Configure(EntityTypeBuilder<CraftingBlueprintTier> builder)
    {
        builder.ToTable("blueprint_tiers", schema: "sc");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.BlueprintId).HasColumnName("blueprint_id").IsRequired();
        builder.Property(t => t.TierIndex).HasColumnName("tier_index").IsRequired();
        builder.Property(t => t.CraftTimeSeconds).HasColumnName("craft_time_seconds").IsRequired();

        builder.HasOne<CraftingBlueprint>()
            .WithMany()
            .HasForeignKey(t => t.BlueprintId)
            .HasConstraintName("fk_blueprint_tiers_blueprint_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.BlueprintId, t.TierIndex })
            .IsUnique()
            .HasDatabaseName("ux_blueprint_tiers_blueprint_id_tier_index");
    }
}
