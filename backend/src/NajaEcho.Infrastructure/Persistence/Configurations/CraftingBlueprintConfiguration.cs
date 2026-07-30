using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NajaEcho.Domain.Blueprints;

namespace NajaEcho.Infrastructure.Persistence.Configurations;

public sealed class CraftingBlueprintConfiguration : IEntityTypeConfiguration<CraftingBlueprint>
{
    public void Configure(EntityTypeBuilder<CraftingBlueprint> builder)
    {
        builder.ToTable("blueprints", schema: "sc");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.Tag).HasColumnName("tag").HasMaxLength(512).IsRequired();
        builder.Property(b => b.ProductEntityClass).HasColumnName("product_entity_class").IsRequired();
        builder.Property(b => b.Gear).HasColumnName("gear").HasMaxLength(256).IsRequired();
        builder.Property(b => b.Type).HasColumnName("type").HasMaxLength(256);
        builder.Property(b => b.Subtype).HasColumnName("subtype").HasMaxLength(256);
        builder.Property(b => b.ProductName).HasColumnName("product_name").HasMaxLength(512);
        builder.Property(b => b.Manufacturer).HasColumnName("manufacturer").HasMaxLength(512);
        builder.Property(b => b.IsDefault).HasColumnName("is_default");
        builder.Property(b => b.SuggestedName).HasColumnName("suggested_name").HasMaxLength(512);
        builder.Property(b => b.SuggestedProductEntityClass).HasColumnName("suggested_product_entity_class");
        builder.Property(b => b.CigDataError).HasColumnName("cig_data_error");

        builder.Property(b => b.Tiers)
            .HasColumnName("tiers")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(b => b.ComponentClass).HasColumnName("component_class").HasMaxLength(64);
        builder.Property(b => b.ComponentSize).HasColumnName("component_size");
        builder.Property(b => b.ComponentGrade).HasColumnName("component_grade").HasMaxLength(8);

        builder.Property(b => b.ImportedAt).HasColumnName("imported_at").IsRequired();
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(b => b.ProductName).HasDatabaseName("ix_blueprints_product_name");
    }
}
