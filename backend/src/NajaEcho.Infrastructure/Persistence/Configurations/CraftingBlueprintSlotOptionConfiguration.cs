using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NajaEcho.Domain.Blueprints;

namespace NajaEcho.Infrastructure.Persistence.Configurations;

public sealed class CraftingBlueprintSlotOptionConfiguration : IEntityTypeConfiguration<CraftingBlueprintSlotOption>
{
    public void Configure(EntityTypeBuilder<CraftingBlueprintSlotOption> builder)
    {
        builder.ToTable("blueprint_slot_options", schema: "sc");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.TierId).HasColumnName("tier_id").IsRequired();
        builder.Property(o => o.SlotIndex).HasColumnName("slot_index").IsRequired();
        builder.Property(o => o.SlotName).HasColumnName("slot_name").HasMaxLength(512).IsRequired();
        builder.Property(o => o.OptionIndex).HasColumnName("option_index").IsRequired();

        builder.Property(o => o.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(o => o.MaterialName).HasColumnName("material_name").HasMaxLength(512).IsRequired();
        builder.Property(o => o.Quantity).HasColumnName("quantity").HasColumnType("numeric").IsRequired();
        builder.Property(o => o.MinQuality).HasColumnName("min_quality").IsRequired();
        builder.Property(o => o.MatchedUexId).HasColumnName("matched_uex_id");

        builder.HasOne<CraftingBlueprintTier>()
            .WithMany()
            .HasForeignKey(o => o.TierId)
            .HasConstraintName("fk_blueprint_slot_options_tier_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => o.TierId).HasDatabaseName("ix_blueprint_slot_options_tier_id");
        builder.HasIndex(o => o.MaterialName).HasDatabaseName("ix_blueprint_slot_options_material_name");
        builder.HasIndex(o => o.MatchedUexId).HasDatabaseName("ix_blueprint_slot_options_matched_uex_id");
        builder.HasIndex(o => new { o.TierId, o.SlotIndex, o.OptionIndex })
            .IsUnique()
            .HasDatabaseName("ux_blueprint_slot_options_tier_slot_option");
    }
}
