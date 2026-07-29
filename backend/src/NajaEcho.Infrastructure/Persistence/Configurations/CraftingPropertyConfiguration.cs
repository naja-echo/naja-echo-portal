using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NajaEcho.Domain.Blueprints;

namespace NajaEcho.Infrastructure.Persistence.Configurations;

public sealed class CraftingPropertyConfiguration : IEntityTypeConfiguration<CraftingProperty>
{
    public void Configure(EntityTypeBuilder<CraftingProperty> builder)
    {
        builder.ToTable("crafting_properties", schema: "sc");
        builder.HasKey(p => p.Key);

        builder.Property(p => p.Key).HasColumnName("property_key").HasMaxLength(256);
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(512).IsRequired();
        builder.Property(p => p.Unit).HasColumnName("unit").HasMaxLength(128);
        builder.Property(p => p.Category).HasColumnName("category").HasMaxLength(256).IsRequired();

        builder.Property(p => p.NameOverrides)
            .HasColumnName("name_overrides")
            .HasColumnType("jsonb");
    }
}
