using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NajaEcho.Domain.Blueprints;

namespace NajaEcho.Infrastructure.Persistence.Configurations;

public sealed class CraftingMaterialConfiguration : IEntityTypeConfiguration<CraftingMaterial>
{
    public void Configure(EntityTypeBuilder<CraftingMaterial> builder)
    {
        builder.ToTable("crafting_materials", schema: "sc");
        builder.HasKey(m => new { m.Kind, m.Name });

        builder.Property(m => m.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(m => m.Name).HasColumnName("name").HasMaxLength(512).IsRequired();
        builder.Property(m => m.MatchedUexId).HasColumnName("matched_uex_id");

        builder.HasIndex(m => m.MatchedUexId).HasDatabaseName("ix_crafting_materials_matched_uex_id");
    }
}
