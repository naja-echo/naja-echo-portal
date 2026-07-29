using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NajaEcho.Domain.Blueprints;

namespace NajaEcho.Infrastructure.Persistence.Configurations;

public sealed class CraftingDatasetConfiguration : IEntityTypeConfiguration<CraftingDataset>
{
    public void Configure(EntityTypeBuilder<CraftingDataset> builder)
    {
        builder.ToTable("crafting_datasets", schema: "sc");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.Version).HasColumnName("version").HasMaxLength(64).IsRequired();
        builder.Property(d => d.TotalBlueprints).HasColumnName("total_blueprints").IsRequired();
        builder.Property(d => d.TotalProducts).HasColumnName("total_products").IsRequired();
        builder.Property(d => d.TotalResources).HasColumnName("total_resources").IsRequired();
        builder.Property(d => d.TotalItems).HasColumnName("total_items").IsRequired();
        builder.Property(d => d.Efficiency).HasColumnName("efficiency").IsRequired();
        builder.Property(d => d.DismantleTimeSeconds).HasColumnName("dismantle_time_seconds").IsRequired();

        builder.Property(d => d.BlacklistedResources)
            .HasColumnName("blacklisted_resources")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(d => d.BlacklistedEntityClasses)
            .HasColumnName("blacklisted_entity_classes")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(d => d.ImportedAt).HasColumnName("imported_at").IsRequired();
    }
}
